using System;
using System.Buffers;
using System.Linq;
using System.Runtime.InteropServices;
using AssetRipper.TextureDecoder.Bc;
using CUE4Parse.Compression;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Textures.ASTC;
using CUE4Parse_Conversion.Textures.BC;
using CUE4Parse_Conversion.Textures.DXT;

namespace CUE4Parse_Conversion.Textures;

public static class TextureDecoder
{
    public static bool UseAssetRipperTextureDecoder { get; set; } = false;

    public static CTexture? Decode(this UTexture2D texture, int maxMipSize, ETexturePlatform platform = ETexturePlatform.DesktopMobile) => texture.Decode(texture.GetMipByMaxSize(maxMipSize), platform);
    public static CTexture? Decode(this UTexture2D texture, ETexturePlatform platform = ETexturePlatform.DesktopMobile) => texture.Decode(texture.GetFirstMip(), platform);
    public static CTexture? Decode(this UTexture texture, ETexturePlatform platform = ETexturePlatform.DesktopMobile) => texture.Decode(texture.GetFirstMip(), platform);
    public static CTexture? Decode(this UTexture texture, FTexture2DMipMap? mip, ETexturePlatform platform = ETexturePlatform.DesktopMobile, int zLayer = 0)
    {
        if (texture.PlatformData is { FirstMipToSerialize: >= 0, VTData: { } vt } && vt.IsInitialized())
            return DecodeVT(texture, vt);

        if (mip == null)
            return null;

        var sizeX = mip.SizeX;
        var sizeY = mip.SizeY;
        var sizeZ = mip.SizeZ;

        if (texture.Format == EPixelFormat.PF_BC7)
        {
            sizeX = sizeX.Align(4);
            sizeY = sizeY.Align(4);
        }

        DecodeTexture(mip, sizeX, sizeY, sizeZ, texture.Format, texture.IsNormalMap, platform, out var data, out var colorType);
        return new CTexture(sizeX, sizeY, colorType, data);
    }

    private static unsafe Span<byte> GetSliceData(byte* data, int sizeX, int sizeY, int bytesPerPixel, int zLayer = 0)
    {
        var offset = sizeX * sizeY * bytesPerPixel;
        var startIndex = offset * zLayer;
        return new Span<byte>(data + startIndex, offset);
    }

    public static int GetMinLevel(FVirtualTextureBuiltData vt)
    {
        for (var i = 0; i < vt.NumMips; i++)
        {
            var tileOffsetData = vt.GetTileOffsetData(i);
            ulong length = tileOffsetData.Height * tileOffsetData.Width * vt.TileSize * vt.TileSize * 4; // for simplicity just use 4 bytes per pixel
            if (length < (ulong)Array.MaxLength)
                return i;
        }
        return 0;
    }

    private static CTexture DecodeVT(UTexture texture, FVirtualTextureBuiltData vt)
    {
        unsafe
        {
            // 检查TileOffsetData数组是否为空
            if (vt.TileOffsetData == null || vt.TileOffsetData.Length == 0)
                throw new ParserException("虚拟纹理图块偏移数据为空或不存在!");

            var tileSize = (int)vt.TileSize;
            var tileBorderSize = (int)vt.TileBorderSize;
            var tilePixelSize = (int)vt.GetPhysicalTileSize();
            int level = GetMinLevel(vt);

            // GetTileOffsetData应在内部处理边界检查
            var tileOffsetData = vt.GetTileOffsetData(level);

            var bitmapWidth = (int)tileOffsetData.Width * tileSize;
            var bitmapHeight = (int)tileOffsetData.Height * tileSize;
            var maxLevel = Math.Ceiling(Math.Log2(Math.Max(tileOffsetData.Width, tileOffsetData.Height)));

            if (tileOffsetData.MaxAddress > 1 && (maxLevel == 0 || vt.IsLegacyData()))
            {
                // 对于旧版数据，我们需要谨慎处理第一个图块偏移数据
                if (vt.TileOffsetData.Length > 0)
                {
                    var firstTileOffsetData = vt.TileOffsetData[0];
                    var baseLevel = vt.IsLegacyData()
                        ? maxLevel
                        : Math.Ceiling(Math.Log2(Math.Max(firstTileOffsetData.Width, firstTileOffsetData.Height)));
                    var factor = Convert.ToInt32(Math.Max(Math.Pow(2, vt.IsLegacyData() ? level : level - baseLevel), 1));
                    bitmapWidth /= factor;
                    bitmapHeight /= factor;
                }
            }

            EPixelFormat colorType = EPixelFormat.PF_Unknown;
            void* pixelDataPtr = null;
            var bytesPerPixel = 0;
            var rowBytes = 0;
            var tileRowBytes = 0;
            var result = Span<byte>.Empty;

            // 检查图层和块
            if (vt.LayerTypes == null)
                throw new ParserException("虚拟纹理图层类型为空!");
            if (vt.Chunks == null)
                throw new ParserException("虚拟纹理块为空!");

            for (uint layer = 0; layer < vt.NumLayers; layer++)
            {
                if (layer >= vt.LayerTypes.Length)
                    throw new ParserException($"图层索引 {layer} 超出范围!");

                var layerFormat = vt.LayerTypes[layer];
                var formatInfo = PixelFormatUtils.PixelFormats.ElementAtOrDefault((int)layerFormat);
                if (formatInfo is not { Supported: true } || formatInfo.BlockBytes == 0)
                    throw new NotImplementedException($"不支持的像素格式 {layerFormat}!");

                var tileWidthInBlocks = tilePixelSize.DivideAndRoundUp(formatInfo.BlockSizeX);
                var tileHeightInBlocks = tilePixelSize.DivideAndRoundUp(formatInfo.BlockSizeY);
                var packedStride = tileWidthInBlocks * formatInfo.BlockBytes;
                var packedOutputSize = packedStride * tileHeightInBlocks;

                var layerData = ArrayPool<byte>.Shared.Rent(packedOutputSize);

                for (uint tileIndexInMip = 0; tileIndexInMip < tileOffsetData.MaxAddress; tileIndexInMip++)
                {
                    if (!vt.IsValidAddress(level, tileIndexInMip)) continue;

                    var (chunkIndex, tileStart, tileLength) = vt.GetTileData(level, tileIndexInMip, layer);
                    if (chunkIndex >= vt.Chunks.Length)
                        throw new ParserException($"无效的块索引 {chunkIndex}!");

                    var chunk = vt.Chunks[chunkIndex];
                    if (chunk.BulkData?.Data == null)
                        throw new ParserException($"块 {chunkIndex} 的批量数据为空!");

                    if (chunk.CodecType == null || layer >= chunk.CodecType.Length)
                        throw new ParserException($"图层 {layer} 的编解码类型无效!");

                    var tileX = (int)MathUtils.ReverseMortonCode2(tileIndexInMip) * tileSize;
                    var tileY = (int)MathUtils.ReverseMortonCode2(tileIndexInMip >> 1) * tileSize;

                    if (chunk.CodecType[layer] == EVirtualTextureCodec.ZippedGPU_DEPRECATED)
                        Compression.Decompress(chunk.BulkData.Data, (int)tileStart, (int)tileLength, layerData, 0, packedOutputSize, CompressionMethod.Zlib);
                    else
                        Array.Copy(chunk.BulkData.Data, (int)tileStart, layerData, 0, (int)Math.Min(packedOutputSize, chunk.BulkData.Data.Length - tileStart));

                    DecodeBytes(layerData, tilePixelSize, tilePixelSize, 1, formatInfo, texture.IsNormalMap, out var data, out var tileColorType);

                    if (pixelDataPtr is null)
                    {
                        colorType = tileColorType;
                        var tempFormatInfo = PixelFormatUtils.PixelFormats.ElementAtOrDefault((int)tileColorType);
                        if (tempFormatInfo == null)
                            throw new ParserException($"不支持的颜色类型 {tileColorType}!");

                        bytesPerPixel = tempFormatInfo.BlockBytes / (tempFormatInfo.BlockSizeX * tempFormatInfo.BlockSizeY * tempFormatInfo.BlockSizeZ);
                        rowBytes = bytesPerPixel * bitmapWidth;
                        tileRowBytes = tileSize * bytesPerPixel;
                        var imageBytes = bitmapHeight * bitmapWidth * bytesPerPixel;
                        pixelDataPtr = NativeMemory.Alloc((nuint)imageBytes);
                        result = new Span<byte>(pixelDataPtr, imageBytes);
                    }
                    else if (colorType != tileColorType)
                    {
                        throw new NotSupportedException("单个虚拟图像中存在多种像素格式/颜色类型不被支持");
                    }

                    for (int i = 0; i < tileSize; i++)
                    {
                        var tileOffset = ((i + tileBorderSize) * tilePixelSize + tileBorderSize) * bytesPerPixel;
                        var offset = tileX * bytesPerPixel + (tileY + i) * rowBytes;
                        var srcSpan = data.AsSpan(tileOffset, tileRowBytes);
                        var destSpan = result.Slice(offset, tileRowBytes);
                        srcSpan.CopyTo(destSpan);
                    }
                }

                ArrayPool<byte>.Shared.Return(layerData);
            }

            if (pixelDataPtr == null)
                throw new ParserException("解码虚拟纹理数据失败!");

            var managedData = GetSliceData((byte*)pixelDataPtr, bitmapWidth, bitmapHeight, bytesPerPixel).ToArray();
            NativeMemory.Free(pixelDataPtr);

            return new CTexture(bitmapWidth, bitmapHeight, colorType, managedData);
        }
    }

    public static unsafe CTexture[]? DecodeTextureArray(this UTexture2DArray texture, ETexturePlatform platform = ETexturePlatform.DesktopMobile)
    {
        var mip = texture.GetFirstMip();

        if (mip is null)
            return null;

        var sizeX = mip.SizeX;
        var sizeY = mip.SizeY;
        var sizeZ = mip.SizeZ;

        if (texture.Format == EPixelFormat.PF_BC7)
        {
            sizeX = sizeX.Align(4);
            sizeY = sizeY.Align(4);
        }

        DecodeTexture(mip, sizeX, sizeY, sizeZ, texture.Format, texture.IsNormalMap, platform, out var data, out var colorType);

        var bitmaps = new CTexture[sizeZ];
        var offset = sizeX * sizeY * 4;

        fixed (byte* dataPtr = data)
        {
            for (var i = 0; i < sizeZ; i++)
            {
                if (offset * (i + 1) > data.Length)
                    break;
                bitmaps[i] = new CTexture(sizeX, sizeY, colorType, GetSliceData(dataPtr, sizeX, sizeY, 4, i).ToArray());
            }
        }
        return bitmaps;
    }

    private static void DecodeTexture(FTexture2DMipMap? mip, int sizeX, int sizeY, int sizeZ, EPixelFormat format, bool isNormalMap, ETexturePlatform platform, out byte[] data, out EPixelFormat colorType)
    {
        if (mip is null || mip.BulkData is null || mip.BulkData.Data is not { Length: > 0 })
            throw new ParserException("提供的MipMap为空或数据为空!");

        var formatInfo = PixelFormatUtils.PixelFormats.ElementAtOrDefault((int)format);
        if (formatInfo is not { Supported: true } || formatInfo.BlockBytes == 0)
            throw new NotImplementedException($"不支持的像素格式 {format}!");

        var isXBPS = platform == ETexturePlatform.XboxAndPlaystation;
        var isNX = platform == ETexturePlatform.NintendoSwitch;

        // 如果平台需要解块，检查我们是否应该尝试
        if (isXBPS || isNX)
        {
            var blockSizeX = mip.SizeX / formatInfo.BlockSizeX;
            var blockSizeY = mip.SizeY / formatInfo.BlockSizeY;
            var totalBlocks = mip.BulkData.Data.Length / formatInfo.BlockBytes;
            if (blockSizeX * blockSizeY > totalBlocks)
                throw new ParserException("提供的MipMap无法解块!");
        }

        var bytes = mip.BulkData.Data;

        // 必要时处理解块
        if (isXBPS)
            bytes = PlatformDeswizzlers.DeswizzleXBPS(bytes, mip, formatInfo);
        else if (isNX)
            bytes = PlatformDeswizzlers.GetDeswizzledData(bytes, mip, formatInfo);

        DecodeBytes(bytes, sizeX, sizeY, sizeZ, formatInfo, isNormalMap, out data, out colorType);
    }

    private static void DecodeBytes(byte[] bytes, int sizeX, int sizeY, int sizeZ, FPixelFormatInfo formatInfo, bool isNormalMap, out byte[] data, out EPixelFormat colorType)
    {
        // 默认返回原始数据和格式
        data = bytes;
        colorType = formatInfo.UnrealFormat;

        switch (formatInfo.UnrealFormat)
        {
            case EPixelFormat.PF_DXT1:
                {
                    if (UseAssetRipperTextureDecoder)
                    {
                        Bc1.Decompress(bytes, sizeX, sizeY, out data);
                        colorType = EPixelFormat.PF_B8G8R8A8;
                    }
                    else
                    {
                        data = DXTDecoder.DXT1(bytes, sizeX, sizeY, sizeZ);
                        colorType = EPixelFormat.PF_R8G8B8A8;
                    }
                    break;
                }
            case EPixelFormat.PF_DXT5:
                if (UseAssetRipperTextureDecoder)
                {
                    Bc3.Decompress(bytes, sizeX, sizeY, out data);
                    colorType = EPixelFormat.PF_B8G8R8A8;
                }
                else
                {
                    data = DXTDecoder.DXT5(bytes, sizeX, sizeY, sizeZ);
                    colorType = EPixelFormat.PF_R8G8B8A8;
                }
                break;
            case EPixelFormat.PF_ASTC_4x4:
            case EPixelFormat.PF_ASTC_6x6:
            case EPixelFormat.PF_ASTC_8x8:
            case EPixelFormat.PF_ASTC_10x10:
            case EPixelFormat.PF_ASTC_12x12:
                data = ASTCDecoder.RGBA8888(bytes, formatInfo.BlockSizeX, formatInfo.BlockSizeY, formatInfo.BlockSizeZ, sizeX, sizeY, sizeZ);
                colorType = EPixelFormat.PF_R8G8B8A8;

                if (isNormalMap)
                {
                    // UE4在编码前会丢弃法线贴图的蓝色通道，恢复它
                    unsafe
                    {
                        var offset = 0;
                        fixed (byte* d = data)
                        {
                            for (var i = 0; i < sizeX * sizeY; i++)
                            {
                                d[offset + 2] = BCDecoder.GetZNormal(d[offset], d[offset + 1]);
                                offset += 4;
                            }
                        }
                    }
                }

                break;
            case EPixelFormat.PF_BC4:
                if (UseAssetRipperTextureDecoder)
                    Bc4.Decompress(bytes, sizeX, sizeY, out data);
                else
                    data = BCDecoder.BC4(bytes, sizeX, sizeY, sizeZ);
                colorType = EPixelFormat.PF_B8G8R8A8;
                break;
            case EPixelFormat.PF_BC5:
                if (UseAssetRipperTextureDecoder)
                    Bc5.Decompress(bytes, sizeX, sizeY, out data);
                else
                    data = BCDecoder.BC5(bytes, sizeX, sizeY, sizeZ);
                for (var i = 0; i < sizeX * sizeY; i++)
                    data[i * 4] = BCDecoder.GetZNormal(data[i * 4 + 2], data[i * 4 + 1]);
                colorType = EPixelFormat.PF_B8G8R8A8;
                break;
            case EPixelFormat.PF_BC6H:
                if (UseAssetRipperTextureDecoder)
                {
                    Bc6h.Decompress(bytes, sizeX, sizeY, false, out data);
                    colorType = EPixelFormat.PF_B8G8R8A8;
                }
                else
                {
                    // BC6H无论像素格式如何都不起作用，我们能得到的最接近的是
                    // Rgb565 DETEX_PIXEL_FORMAT_FLOAT_RGBX16 或 Rgb565 DETEX_PIXEL_FORMAT_FLOAT_BGRX16
                    data = DetexHelper.DecodeDetexLinear(bytes, sizeX, sizeY, true, DetexTextureFormat.DETEX_TEXTURE_FORMAT_BPTC_FLOAT, DetexPixelFormat.DETEX_PIXEL_FORMAT_FLOAT_RGBX16);
                    colorType = EPixelFormat.PF_FloatRGBA; //TODO 不确定
                }
                break;
            case EPixelFormat.PF_BC7:
                if (UseAssetRipperTextureDecoder)
                    Bc7.Decompress(bytes, sizeX, sizeY, out data);
                else
                    data = DetexHelper.DecodeDetexLinear(bytes, sizeX, sizeY * sizeZ, false, DetexTextureFormat.DETEX_TEXTURE_FORMAT_BPTC, DetexPixelFormat.DETEX_PIXEL_FORMAT_BGRA8);
                colorType = EPixelFormat.PF_B8G8R8A8;
                break;
            case EPixelFormat.PF_ETC1:
                data = DetexHelper.DecodeDetexLinear(bytes, sizeX, sizeY, false, DetexTextureFormat.DETEX_TEXTURE_FORMAT_ETC1, DetexPixelFormat.DETEX_PIXEL_FORMAT_BGRA8);
                colorType = EPixelFormat.PF_B8G8R8A8;
                break;
            case EPixelFormat.PF_ETC2_RGB:
                data = DetexHelper.DecodeDetexLinear(bytes, sizeX, sizeY, false, DetexTextureFormat.DETEX_TEXTURE_FORMAT_ETC2, DetexPixelFormat.DETEX_PIXEL_FORMAT_BGRA8);
                colorType = EPixelFormat.PF_B8G8R8A8;
                break;
            case EPixelFormat.PF_ETC2_RGBA:
                data = DetexHelper.DecodeDetexLinear(bytes, sizeX, sizeY, false, DetexTextureFormat.DETEX_TEXTURE_FORMAT_ETC2_EAC, DetexPixelFormat.DETEX_PIXEL_FORMAT_BGRA8);
                colorType = EPixelFormat.PF_B8G8R8A8;
                break;

            // 部分:原始格式。不做处理，我们返回原始格式和数据
            case EPixelFormat.PF_B8G8R8A8:
            case EPixelFormat.PF_G8:
            case EPixelFormat.PF_A32B32G32R32F:
            case EPixelFormat.PF_FloatRGB:
            case EPixelFormat.PF_FloatRGBA:
            case EPixelFormat.PF_R32_FLOAT:
            case EPixelFormat.PF_G16R16F:
            case EPixelFormat.PF_G16R16:
            case EPixelFormat.PF_G32R32F:
            case EPixelFormat.PF_A16B16G16R16:
            case EPixelFormat.PF_R16F:
            case EPixelFormat.PF_G16:
            case EPixelFormat.PF_R32G32B32F:
                break;

            case EPixelFormat.PF_R16F_FILTER:
                colorType = EPixelFormat.PF_R16F;
                break;
            case EPixelFormat.PF_G16R16F_FILTER:
                colorType = EPixelFormat.PF_G16R16F;
                break;

            default:
                throw new NotImplementedException($"未知像素格式: {formatInfo.UnrealFormat}");
        }
    }
}