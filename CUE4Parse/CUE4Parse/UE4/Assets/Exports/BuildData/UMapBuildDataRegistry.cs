using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.BuildData;

public class UMapBuildDataRegistry : UObject
{
    public Dictionary<FGuid, FMeshMapBuildData>? MeshBuildData;
    public Dictionary<FGuid, FPrecomputedLightVolumeData>? LevelPrecomputedLightVolumeBuildData;
    public Dictionary<FGuid, FPrecomputedVolumetricLightmapData>? LevelPrecomputedVolumetricLightmapBuildData;
    public Dictionary<FGuid, FLightComponentMapBuildData>? LightBuildData;
    public Dictionary<FGuid, FReflectionCaptureMapBuildData>? ReflectionCaptureBuildData;
    public Dictionary<FGuid, FSkyAtmosphereMapBuildData>? SkyAtmosphereBuildData;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        var stripFlags = new FStripDataFlags(Ar);

        if (!stripFlags.IsAudioVisualDataStripped())
        {
            MeshBuildData = Ar.ReadMap(Ar.Read<FGuid>, () => new FMeshMapBuildData(Ar));
            LevelPrecomputedLightVolumeBuildData = Ar.ReadMap(Ar.Read<FGuid>, () => new FPrecomputedLightVolumeData(Ar));

            if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.VolumetricLightmaps)
            {
                LevelPrecomputedVolumetricLightmapBuildData = Ar.ReadMap(Ar.Read<FGuid>, () => new FPrecomputedVolumetricLightmapData(Ar));
            }

            LightBuildData = Ar.ReadMap(Ar.Read<FGuid>, () => new FLightComponentMapBuildData(Ar));
            if (FReflectionCaptureObjectVersion.Get(Ar) >= FReflectionCaptureObjectVersion.Type.MoveReflectionCaptureDataToMapBuildData)
            {
                ReflectionCaptureBuildData = Ar.ReadMap(Ar.Read<FGuid>, () => new FReflectionCaptureMapBuildData(Ar));
            }

            if (Ar.Game == EGame.GAME_霍格沃茨遗产)
            {
                Ar.SkipFixedArray(1);
                return;
            }

            if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.SkyAtmosphereStaticLightingVersioning)
            {
                SkyAtmosphereBuildData = Ar.ReadMap(Ar.Read<FGuid>, () => new FSkyAtmosphereMapBuildData(Ar));
            }
        }
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        if (MeshBuildData?.Count > 0)
        {
            writer.WritePropertyName("MeshBuildData");
            serializer.Serialize(writer, MeshBuildData);
        }

        if (LevelPrecomputedLightVolumeBuildData?.Count > 0)
        {
            writer.WritePropertyName("LevelPrecomputedLightVolumeBuildData");
            serializer.Serialize(writer, LevelPrecomputedLightVolumeBuildData);
        }

        if (LevelPrecomputedVolumetricLightmapBuildData?.Count > 0)
        {
            writer.WritePropertyName("LevelPrecomputedVolumetricLightmapBuildData");
            serializer.Serialize(writer, LevelPrecomputedVolumetricLightmapBuildData);
        }

        if (LightBuildData?.Count > 0)
        {
            writer.WritePropertyName("LightBuildData");
            serializer.Serialize(writer, LightBuildData);
        }

        if (ReflectionCaptureBuildData?.Count > 0)
        {
            writer.WritePropertyName("ReflectionCaptureBuildData");
            serializer.Serialize(writer, ReflectionCaptureBuildData);
        }

        if (SkyAtmosphereBuildData?.Count > 0)
        {
            writer.WritePropertyName("SkyAtmosphereBuildData");
            serializer.Serialize(writer, SkyAtmosphereBuildData);
        }
    }
}

public class FSkyAtmosphereMapBuildData
{
    public FSkyAtmosphereMapBuildData(FArchive Ar)
    {
    }
}

public class FReflectionCaptureMapBuildData : FReflectionCaptureData
{
    public FReflectionCaptureMapBuildData(FAssetArchive Ar) : base(Ar) { }
}

[JsonConverter(typeof(FReflectionCaptureDataConverter))]
public class FReflectionCaptureData
{
    public int CubemapSize;
    public float AverageBrightness;
    public float Brightness;
    public byte[]? FullHDRCapturedData;
    public FPackageIndex? EncodedCaptureData;

    public FReflectionCaptureData(FAssetArchive Ar)
    {
        CubemapSize = Ar.Read<int>();
        AverageBrightness = Ar.Read<float>();

        if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.StoreReflectionCaptureBrightnessForCooking &&
            FUE5ReleaseStreamObjectVersion.Get(Ar) < FUE5ReleaseStreamObjectVersion.Type.ExcludeBrightnessFromEncodedHDRCubemap)
        {
            Brightness = Ar.Read<float>();
        }

        Ar.SkipFixedArray(1);
        if (Ar.Game == EGame.GAME_最终幻想7重生) Ar.Position += 4;

        if (FMobileObjectVersion.Get(Ar) >= FMobileObjectVersion.Type.StoreReflectionCaptureCompressedMobile &&
            FUE5ReleaseStreamObjectVersion.Get(Ar) < FUE5ReleaseStreamObjectVersion.Type.StoreReflectionCaptureEncodedHDRDataInRG11B10Format)
        {
            EncodedCaptureData = new FPackageIndex(Ar);
        }
        else
        {
            Ar.SkipFixedArray(1);
        }

        if (Ar.Game == EGame.GAME_第一后裔) Ar.Position += 16;
        if (Ar.Game == EGame.GAME_黑神话_悟空)
        {
            Ar.SkipFixedArray(1);
            Ar.Position += 4;
        }
    }
}

public class FLightComponentMapBuildData
{
    public int ShadowMapChannel;
    public FStaticShadowDepthMapData DepthMap;

    public FLightComponentMapBuildData(FArchive Ar)
    {
        ShadowMapChannel = Ar.Read<int>();
        DepthMap = new FStaticShadowDepthMapData(Ar);
    }
}

public class FStaticShadowDepthMapData
{
    public FMatrix WorldToLight;
    public int ShadowMapSizeX;
    public int ShadowMapSizeY;
    public FFloat16[] DepthSamples;

    public FStaticShadowDepthMapData(FArchive Ar)
    {
        WorldToLight = new FMatrix(Ar);
        ShadowMapSizeX = Ar.Read<int>();
        ShadowMapSizeY = Ar.Read<int>();
        DepthSamples = Ar.ReadArray(() => new FFloat16(Ar));
    }
}

public class FVolumeLightingSample
{
    public FVector Position;
    public float Radius;
    public float[][] Lighting;
    public FColor PackedSkyBentNormal;
    public float DirectionalLightShadowing;

    public FVolumeLightingSample(FAssetArchive Ar)
    {
        Position = Ar.Read<FVector>();
        Radius = Ar.Read<float>();
        Lighting = Ar.ReadArray(3, () => Ar.ReadArray<float>(9));
        PackedSkyBentNormal = Ar.Read<FColor>();
        DirectionalLightShadowing = Ar.Read<float>();
    }
}

public class FPrecomputedLightVolumeData
{
    public FBox Bounds;
    public float SampleSpacing;
    public int NumSHSamples;
    public FVolumeLightingSample[] HighQualitySamples;
    public FVolumeLightingSample[]? LowQualitySamples;

    public FPrecomputedLightVolumeData(FAssetArchive Ar)
    {
        HighQualitySamples = Array.Empty<FVolumeLightingSample>(); // 初始化非空字段

        var bValid = Ar.ReadBoolean();

        if (bValid)
        {
            var bVolumeInitialized = Ar.ReadBoolean();
            if (bVolumeInitialized)
            {
                Bounds = new FBox(Ar);
                SampleSpacing = Ar.Read<float>();
                NumSHSamples = 4;
                if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.IndirectLightingCache3BandSupport)
                {
                    NumSHSamples = Ar.Read<int>();
                }

                HighQualitySamples = Ar.ReadArray(() => new FVolumeLightingSample(Ar));
                if (Ar.Ver >= EUnrealEngineObjectUE4Version.VOLUME_SAMPLE_LOW_QUALITY_SUPPORT)
                {
                    LowQualitySamples = Ar.ReadArray(() => new FVolumeLightingSample(Ar));
                }
            }
        }
    }
}

public class FPrecomputedVolumetricLightmapData
{
    public FBox Bounds;
    public FIntVector IndirectionTextureDimensions;
    public FVolumetricLightmapDataLayer IndirectionTexture;
    public int BrickSize;
    public FIntVector BrickDataDimensions;
    public FVolumetricLightmapBrickLayer BrickData;
    public FIntVector[]? SubLevelBrickPositions;
    public FColor[]? IndirectionTextureOriginalValues;

    public FPrecomputedVolumetricLightmapData(FArchive Ar)
    {
        IndirectionTexture = new FVolumetricLightmapDataLayer(new FByteArchive("Empty", Array.Empty<byte>()));
        BrickData = new FVolumetricLightmapBrickLayer();

        var bValid = Ar.ReadBoolean();

        if (bValid)
        {
            if (Ar.Game == EGame.GAME_星球大战绝地_幸存者) Ar.Position += 8;

            Bounds = new FBox(Ar);
            IndirectionTextureDimensions = Ar.Read<FIntVector>();
            IndirectionTexture = new FVolumetricLightmapDataLayer(Ar);

            BrickSize = Ar.Read<int>();
            BrickDataDimensions = Ar.Read<FIntVector>();

            BrickData = new FVolumetricLightmapBrickLayer
            {
                AmbientVector = new FVolumetricLightmapDataLayer(Ar),
                SHCoefficients = new FVolumetricLightmapDataLayer[6]
            };

            for (var i = 0; i < BrickData.SHCoefficients.Length; i++)
            {
                BrickData.SHCoefficients[i] = new FVolumetricLightmapDataLayer(Ar);
            }

            BrickData.SkyBentNormal = new FVolumetricLightmapDataLayer(Ar);
            BrickData.DirectionalLightShadowing = new FVolumetricLightmapDataLayer(Ar);

            if (FMobileObjectVersion.Get(Ar) >= FMobileObjectVersion.Type.LQVolumetricLightmapLayers)
            {
                if (FUE5MainStreamObjectVersion.Get(Ar) <= FUE5MainStreamObjectVersion.Type.MobileStationaryLocalLights)
                {
                    BrickData.LQLightColor = new FVolumetricLightmapDataLayer(Ar);
                    BrickData.LQLightDirection = new FVolumetricLightmapDataLayer(Ar);
                }
            }

            if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.VolumetricLightmapStreaming)
            {
                SubLevelBrickPositions = Ar.ReadArray<FIntVector>();
                IndirectionTextureOriginalValues = Ar.ReadArray<FColor>();
            }

            if (Ar.Game == EGame.GAME_双影奇境) Ar.Position += 8;
        }
        else
        {
            Bounds = new FBox();
            IndirectionTextureDimensions = new FIntVector();
            BrickSize = 0;
            BrickDataDimensions = new FIntVector();

            BrickData.AmbientVector = new FVolumetricLightmapDataLayer(new FByteArchive("Empty", Array.Empty<byte>()));
            BrickData.SHCoefficients = new FVolumetricLightmapDataLayer[6];
            for (int i = 0; i < 6; i++)
            {
                BrickData.SHCoefficients[i] = new FVolumetricLightmapDataLayer(new FByteArchive("Empty", Array.Empty<byte>()));
            }
            BrickData.SkyBentNormal = new FVolumetricLightmapDataLayer(new FByteArchive("Empty", Array.Empty<byte>()));
            BrickData.DirectionalLightShadowing = new FVolumetricLightmapDataLayer(new FByteArchive("Empty", Array.Empty<byte>()));

            SubLevelBrickPositions = Array.Empty<FIntVector>();
            IndirectionTextureOriginalValues = Array.Empty<FColor>();
        }
    }
}

public class FVolumetricLightmapBasicBrickDataLayers
{
    public FVolumetricLightmapDataLayer? AmbientVector;
    public FVolumetricLightmapDataLayer[]? SHCoefficients;
    public FVolumetricLightmapDataLayer? SkyBentNormal;
    public FVolumetricLightmapDataLayer? DirectionalLightShadowing;
}

public class FVolumetricLightmapBrickLayer : FVolumetricLightmapBasicBrickDataLayers
{
    // Mobile LQ Layers:
    public FVolumetricLightmapDataLayer? LQLightColor;
    public FVolumetricLightmapDataLayer? LQLightDirection;
}

public class FVolumetricLightmapDataLayer
{
    public byte[] Data;
    public string PixelFormatString;

    public FVolumetricLightmapDataLayer(FArchive Ar)
    {
        Data = Ar.ReadArray<byte>();
        PixelFormatString = Ar.ReadFString();
    }
}

[JsonConverter(typeof(FMeshMapBuildDataConverter))]
public class FMeshMapBuildData
{
    public FLightMap? LightMap;
    public FShadowMap? ShadowMap;
    public FGuid[] IrrelevantLights;
    public FPerInstanceLightmapData[] PerInstanceLightmapData;

    public FMeshMapBuildData()
    {
        IrrelevantLights = Array.Empty<FGuid>();
        PerInstanceLightmapData = Array.Empty<FPerInstanceLightmapData>();
    }

    public FMeshMapBuildData(FAssetArchive Ar)
    {
        LightMap = Ar.Read<ELightMapType>() switch
        {
            ELightMapType.LMT_1D => new FLegacyLightMap1D(Ar),
            ELightMapType.LMT_2D => new FLightMap2D(Ar),
            _ => null
        };

        ShadowMap = Ar.Read<EShadowMapType>() switch
        {
            EShadowMapType.SMT_2D => new FShadowMap2D(Ar),
            _ => null
        };

        IrrelevantLights = Ar.ReadArray<FGuid>();
        PerInstanceLightmapData = Ar.ReadBulkArray<FPerInstanceLightmapData>();
    }
}

public enum ELightMapType : uint
{
    LMT_None = 0,
    LMT_1D = 1,
    LMT_2D = 2,
}

public class FLightMap
{
    public FGuid[] LightGuids;

    public FLightMap(FAssetArchive Ar)
    {
        LightGuids = Ar.ReadArray<FGuid>();
    }
}

public class FLegacyLightMap1D : FLightMap
{
    public FLegacyLightMap1D(FAssetArchive Ar) : base(Ar)
    {
        throw new ParserException("不支持:FLegacyLightMap1D");
    }
}

[JsonConverter(typeof(FLightMap2DConverter))]
public class FLightMap2D : FLightMap
{
    const int NUM_STORED_LIGHTMAP_COEF = 4;
    public readonly FPackageIndex[]? Textures;
    public readonly FPackageIndex? SkyOcclusionTexture;
    public readonly FPackageIndex? AOMaterialMaskTexture;
    public readonly FPackageIndex? ShadowMapTexture;
    public readonly FPackageIndex[]? VirtualTextures;
    public readonly FVector4[]? ScaleVectors;
    public readonly FVector4[]? AddVectors;
    public readonly FVector2D? CoordinateScale;
    public readonly FVector2D? CoordinateBias;
    public readonly FVector4? InvUniformPenumbraSize;
    public readonly bool[]? bShadowChannelValid;

    public FLightMap2D(FAssetArchive Ar) : base(Ar)
    {
        Textures = new FPackageIndex[2];
        VirtualTextures = new FPackageIndex[2];
        ScaleVectors = new FVector4[NUM_STORED_LIGHTMAP_COEF];
        AddVectors = new FVector4[NUM_STORED_LIGHTMAP_COEF];

        if (Ar.Ver <= EUnrealEngineObjectUE4Version.LOW_QUALITY_DIRECTIONAL_LIGHTMAPS)
        {
            for (var CoefficientIndex = 0; CoefficientIndex < 3; CoefficientIndex++)
            {
                Ar.Position += 36;
            }
        }
        else if (Ar.Ver <= EUnrealEngineObjectUE4Version.COMBINED_LIGHTMAP_TEXTURES)
        {
            for (var CoefficientIndex = 0; CoefficientIndex < 4; CoefficientIndex++)
            {
                Ar.Position += 36;
            }
        }
        else
        {
            Textures[0] = new FPackageIndex(Ar);
            Textures[1] = new FPackageIndex(Ar);

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.SKY_LIGHT_COMPONENT)
            {
                SkyOcclusionTexture = new FPackageIndex(Ar);
                if (Ar.Ver >= EUnrealEngineObjectUE4Version.AO_MATERIAL_MASK)
                {
                    AOMaterialMaskTexture = new FPackageIndex(Ar);
                }
            }

            for (var CoefficientIndex = 0; CoefficientIndex < NUM_STORED_LIGHTMAP_COEF; CoefficientIndex++)
            {
                ScaleVectors[CoefficientIndex] = Ar.Read<FVector4>();
                AddVectors[CoefficientIndex] = Ar.Read<FVector4>();
            }
        }

        CoordinateScale = new FVector2D(Ar);
        CoordinateBias = new FVector2D(Ar);

        if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.LightmapHasShadowmapData)
        {
            bShadowChannelValid = Ar.ReadArray(4, () => Ar.ReadBoolean());
            InvUniformPenumbraSize = Ar.Read<FVector4>();
        }

        if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.VirtualTexturedLightmaps)
        {
            if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.VirtualTexturedLightmapsV2)
            {
                if (FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.VirtualTexturedLightmapsV3)
                {
                    VirtualTextures[0] = new FPackageIndex(Ar);
                    VirtualTextures[1] = new FPackageIndex(Ar);
                }
                else
                {
                    VirtualTextures[0] = new FPackageIndex(Ar);
                }
            }
            else
            {
                VirtualTextures[0] = new FPackageIndex(Ar);
            }
        }

        if (Ar.Game == EGame.GAME_巅峰极速) Ar.Position += 20;
        if (Ar.Game == EGame.GAME_地铁觉醒) Ar.Position += 4;
    }
}

public enum EShadowMapType : uint
{
    SMT_None = 0,
    SMT_2D = 2,
}

public class FShadowMap
{
    public readonly FGuid[] LightGuids;

    public FShadowMap(FAssetArchive Ar)
    {
        LightGuids = Ar.ReadArray<FGuid>();
    }
}

public class FShadowMap2D : FShadowMap
{
    public readonly FPackageIndex Texture;
    public readonly FVector2D CoordinateScale;
    public readonly FVector2D CoordinateBias;
    public readonly bool[] bChannelValid;
    public readonly FVector4 InvUniformPenumbraSize;

    public FShadowMap2D(FAssetArchive Ar) : base(Ar)
    {
        Texture = new FPackageIndex(Ar);
        CoordinateScale = new FVector2D(Ar);
        CoordinateBias = new FVector2D(Ar);
        bChannelValid = Ar.ReadArray(4, () => Ar.ReadBoolean());

        if (Ar.Ver >= EUnrealEngineObjectUE4Version.STATIC_SHADOWMAP_PENUMBRA_SIZE)
        {
            InvUniformPenumbraSize = Ar.Read<FVector4>();
        }
        else
        {
            const float LegacyValue = 1.0f / .05f;
            InvUniformPenumbraSize = new FVector4(LegacyValue);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct FPerInstanceLightmapData
{
    public readonly FVector2D LightmapUVBias;
    public readonly FVector2D ShadowmapUVBias;
}