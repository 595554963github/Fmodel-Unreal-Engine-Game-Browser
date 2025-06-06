using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CUE4Parse.UE4.Assets.Exports.Texture
{
    public abstract class UTexture : UUnrealMaterial, IAssetUserData
    {
        public FGuid LightingGuid { get; private set; }
        public TextureCompressionSettings CompressionSettings { get; private set; }
        public bool SRGB { get; private set; }
        public FPackageIndex[] AssetUserData { get; private set; } = Array.Empty<FPackageIndex>();
        public bool RenderNearestNeighbor { get; private set; }
        public EPixelFormat Format { get; protected set; } = EPixelFormat.PF_Unknown;
        public FTexturePlatformData? PlatformData { get; private set; }

        public bool IsNormalMap => CompressionSettings == TextureCompressionSettings.TC_Normalmap;
        public bool IsHDR => CompressionSettings is
            TextureCompressionSettings.TC_HDR or
            TextureCompressionSettings.TC_HDR_F32 or
            TextureCompressionSettings.TC_HDR_Compressed or
            TextureCompressionSettings.TC_HalfFloat or
            TextureCompressionSettings.TC_SingleFloat;

        public virtual TextureAddress GetTextureAddressX() => TextureAddress.TA_Wrap;
        public virtual TextureAddress GetTextureAddressY() => TextureAddress.TA_Wrap;
        public virtual TextureAddress GetTextureAddressZ() => TextureAddress.TA_Wrap;

        private readonly Dictionary<Type, object?> _assetUserDataCache = new Dictionary<Type, object?>();

        public UTextureAllMipDataProviderFactory? MipDataProvider
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetAssetUserData<UTextureAllMipDataProviderFactory>();
        }

        protected T? GetAssetUserData<T>() where T : class
        {
            var targetType = typeof(T);

            if (_assetUserDataCache.TryGetValue(targetType, out var cachedObj))
            {
                return cachedObj as T;
            }

            foreach (var audRef in AssetUserData)
            {
                try
                {
                    var loadedObj = audRef.Load();
                    if (loadedObj == null) continue;

                    if (targetType.IsInstanceOfType(loadedObj))
                    {
                        _assetUserDataCache[targetType] = loadedObj;
                        return loadedObj as T;
                    }

                    Log.Verbose($"AssetUserData包含不匹配的类型: {loadedObj.GetType().FullName} (期望 {targetType.FullName})");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"加载AssetUserData时发生异常");
                }
            }

            _assetUserDataCache[targetType] = null;
            Log.Warning($"在AssetUserData中未找到类型 {targetType.FullName} 的对象");
            return null;
        }

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);
            LightingGuid = GetOrDefault(nameof(LightingGuid), new FGuid((uint)GetFullName().GetHashCode()));
            CompressionSettings = GetOrDefault(nameof(CompressionSettings), TextureCompressionSettings.TC_Default);
            SRGB = GetOrDefault(nameof(SRGB), true);
            AssetUserData = GetOrDefault(nameof(AssetUserData), Array.Empty<FPackageIndex>());

            if (TryGetValue(out FName trigger, "LODGroup", "Filter") && !trigger.IsNone)
            {
                RenderNearestNeighbor = trigger.Text.EndsWith("TEXTUREGROUP_Pixels2D", StringComparison.OrdinalIgnoreCase) ||
                                        trigger.Text.EndsWith("TF_Nearest", StringComparison.OrdinalIgnoreCase);
            }

            var stripFlags = Ar.Read<FStripDataFlags>();

            if (!stripFlags.IsEditorDataStripped())
            {
                // Editor-only data handling
            }
        }

        protected void DeserializeCookedPlatformData(FAssetArchive Ar, bool bSerializeMipData = true)
        {
            var pixelFormatName = Ar.ReadFName();
            while (!pixelFormatName.IsNone)
            {
                Enum.TryParse(pixelFormatName.Text, out EPixelFormat pixelFormat);

                var skipOffset = Ar.Game switch
                {
                    >= EGame.GAME_UE5_0 => Ar.AbsolutePosition + Ar.Read<long>(),
                    >= EGame.GAME_UE4_20 => Ar.Read<long>(),
                    _ => Ar.Read<int>()
                };

                if (Format == EPixelFormat.PF_Unknown)
                {
                    PlatformData = new FTexturePlatformData(Ar);

                    if (Ar.AbsolutePosition != skipOffset)
                    {
                        Log.Warning($"Texture2D读取不正确.偏移量{Ar.AbsolutePosition},跳过偏移量{skipOffset},剩余字节数{skipOffset - Ar.AbsolutePosition}");
                        Ar.SeekAbsolute(skipOffset, SeekOrigin.Begin);
                    }

                    Format = pixelFormat;
                }
                else
                {
                    Ar.SeekAbsolute(skipOffset, SeekOrigin.Begin);
                }

                pixelFormatName = Ar.ReadFName();
            }
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            if (PlatformData == null) return;

            writer.WritePropertyName("SizeX");
            writer.WriteValue(PlatformData.SizeX);

            writer.WritePropertyName("SizeY");
            writer.WriteValue(PlatformData.SizeY);

            writer.WritePropertyName("PackedData");
            writer.WriteValue(PlatformData.PackedData);

            writer.WritePropertyName("PixelFormat");
            writer.WriteValue(Format.ToString());

            if (PlatformData.OptData.ExtData != 0 && PlatformData.OptData.NumMipsInTail != 0)
            {
                writer.WritePropertyName("OptData");
                serializer.Serialize(writer, PlatformData.OptData);
            }

            writer.WritePropertyName("FirstMipToSerialize");
            writer.WriteValue(PlatformData.FirstMipToSerialize);

            if (PlatformData.Mips is { Length: > 0 })
            {
                writer.WritePropertyName("Mips");
                serializer.Serialize(writer, PlatformData.Mips);
            }

            if (PlatformData.VTData != null)
            {
                writer.WritePropertyName("VTData");
                serializer.Serialize(writer, PlatformData.VTData);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FTexture2DMipMap? GetMip(int index)
        {
            if (PlatformData == null || index < 0 || index >= PlatformData.Mips.Length)
                return null;

            return PlatformData.Mips[index].EnsureValidBulkData(MipDataProvider, index)
                ? PlatformData.Mips[index]
                : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FTexture2DMipMap? GetFirstMip()
        {
            if (PlatformData == null) return null;
            return PlatformData.Mips.FirstOrDefault(mip => mip.EnsureValidBulkData(MipDataProvider, Array.IndexOf(PlatformData.Mips, mip)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FTexture2DMipMap? GetMipByMaxSize(int maxSize)
        {
            if (PlatformData == null) return null;

            for (var i = 0; i < PlatformData.Mips.Length; i++)
            {
                var mip = PlatformData.Mips[i];
                if ((mip.SizeX <= maxSize || mip.SizeY <= maxSize) && mip.EnsureValidBulkData(MipDataProvider, i))
                    return mip;
            }

            return GetFirstMip();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FTexture2DMipMap? GetMipBySize(int sizeX, int sizeY)
        {
            if (PlatformData == null) return null;

            for (var i = 0; i < PlatformData.Mips.Length; i++)
            {
                var mip = PlatformData.Mips[i];
                if (mip.SizeX == sizeX && mip.SizeY == sizeY && mip.EnsureValidBulkData(MipDataProvider, i))
                    return mip;
            }

            return GetFirstMip();
        }

        public override void GetParams(CMaterialParams parameters)
        {
            // Default empty method
        }

        public override void GetParams(CMaterialParams2 parameters, EMaterialFormat format)
        {
            // Default empty method
        }
    }
}