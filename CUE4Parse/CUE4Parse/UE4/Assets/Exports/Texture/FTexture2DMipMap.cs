using System;
using System.Reflection;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Texture
{
    [JsonConverter(typeof(FTexture2DMipMapConverter))]
    public class FTexture2DMipMap
    {
        public FByteBulkData? BulkData;
        public int SizeX;
        public int SizeY;
        public int SizeZ;

        public FTexture2DMipMap(FByteBulkData bulkData, int sizeX, int sizeY, int sizeZ)
        {
            BulkData = bulkData;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
        }

        public FTexture2DMipMap(FAssetArchive Ar, bool bSerializeMipData = true)
        {
            var cooked = Ar.Ver >= EUnrealEngineObjectUE4Version.TEXTURE_SOURCE_ART_REFACTOR && Ar.Game < EGame.GAME_UE5_0
                ? Ar.ReadBoolean()
                : Ar.IsFilterEditorOnly;

            if (bSerializeMipData)
                BulkData = new FByteBulkData(Ar);

            if (Ar.Game == EGame.GAME_无主之地3)
            {
                SizeX = Ar.Read<ushort>();
                SizeY = Ar.Read<ushort>();
                SizeZ = Ar.Read<ushort>();
            }
            else
            {
                SizeX = Ar.Read<int>();
                SizeY = Ar.Read<int>();
                SizeZ = Ar.Game >= EGame.GAME_UE4_20 ? Ar.Read<int>() : 1;
            }

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.TEXTURE_DERIVED_DATA2 && !cooked)
            {
                var derivedDataKey = Ar.ReadFString();
            }
        }

        public bool EnsureValidBulkData(UTextureAllMipDataProviderFactory? provider, int mipLevel)
        {
            if (BulkData?.Data != null)
                return true;

            if (provider == null)
                return false;

            try
            {
                // 使用反射方式
                if (provider.GetType().FullName == "CUE4Parse.UE4.Assets.Exports.Component.Landscape.ULandscapeTextureStorageProviderFactory")
                {
                    var mipsProperty = provider.GetType().GetProperty("Mips");
                    var decompressMethod = provider.GetType().GetMethod("DecompressMip");

                    if (mipsProperty != null && decompressMethod != null)
                    {
                        var mips = mipsProperty.GetValue(provider) as FTexture2DMipMap[];
                        if (mips != null && mipLevel < mips.Length)
                        {
                            var mip = mips[mipLevel];
                            if (mip.BulkData?.Data != null)
                            {
                                var data = new Lazy<byte[]?>(() =>
                                {
                                    var destination = new byte[mip.SizeX * mip.SizeY * 4];
                                    decompressMethod.Invoke(provider, new object[] {
                                        mip.BulkData.Data,
                                        mip.BulkData.Data.Length,
                                        destination,
                                        destination.Length,
                                        mipLevel
                                    });
                                    return destination;
                                });

                                BulkData = new FByteBulkData(data);
                                return true;
                            }
                        }
                    }
                }

                var landscapeProvider = (dynamic)provider;
                var mipsArray = landscapeProvider.Mips as FTexture2DMipMap[];

                if (mipsArray != null && mipLevel >= 0 && mipLevel < mipsArray.Length)
                {
                    var mip = mipsArray[mipLevel];
                    if (mip != null && mip.BulkData?.Data != null)
                    {
                        var data = new Lazy<byte[]?>(() =>
                        {
                            var destination = new byte[mip.SizeX * mip.SizeY * 4];
                            landscapeProvider.DecompressMip(
                                mip.BulkData.Data,
                                mip.BulkData.Data.Length,
                                destination,
                                destination.Length,
                                mipLevel
                            );
                            return destination;
                        });

                        BulkData = new FByteBulkData(data);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解压mip时出错: {ex.Message}");
                return false;
            }

            return false;
        }
    }
}