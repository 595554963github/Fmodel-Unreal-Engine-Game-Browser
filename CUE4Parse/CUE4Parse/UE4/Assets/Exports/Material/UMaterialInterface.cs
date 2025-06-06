using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Material;

[SkipObjectRegistration]
public class UMaterialInterface : UUnrealMaterial
{
    //I think those aren't used in UE4 but who knows
    //to delete
    public bool bUseMobileSpecular;
    public float MobileSpecularPower = 16.0f;
    public EMobileSpecularMask MobileSpecularMask = EMobileSpecularMask.MSM_Constant;
    public UTexture? FlattenedTexture;
    public UTexture? MobileBaseTexture;
    public UTexture? MobileNormalTexture;
    public UTexture? MobileMaskTexture;

    public FStructFallback? CachedExpressionData;
    public FMaterialTextureInfo[] TextureStreamingData = Array.Empty<FMaterialTextureInfo>();
    public List<FMaterialResource> LoadedMaterialResources = new();

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        bUseMobileSpecular = GetOrDefault<bool>(nameof(bUseMobileSpecular));
        MobileSpecularPower = GetOrDefault<float>(nameof(MobileSpecularPower));
        MobileSpecularMask = GetOrDefault<EMobileSpecularMask>(nameof(MobileSpecularMask));
        FlattenedTexture = GetOrDefault<UTexture>(nameof(FlattenedTexture));
        MobileBaseTexture = GetOrDefault<UTexture>(nameof(MobileBaseTexture));
        MobileNormalTexture = GetOrDefault<UTexture>(nameof(MobileNormalTexture));
        MobileMaskTexture = GetOrDefault<UTexture>(nameof(MobileMaskTexture));

        TextureStreamingData = GetOrDefault(nameof(TextureStreamingData), Array.Empty<FMaterialTextureInfo>());

        var bSavedCachedExpressionData = FUE5ReleaseStreamObjectVersion.Get(Ar) >= FUE5ReleaseStreamObjectVersion.Type.MaterialInterfaceSavedCachedData && Ar.ReadBoolean();
        if (bSavedCachedExpressionData)
        {
            CachedExpressionData = new FStructFallback(Ar, "MaterialCachedExpressionData");
        }

        if (Ar.Game == EGame.GAME_霍格沃茨遗产) Ar.Position += 20; // FSHAHash
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        if (LoadedMaterialResources is not null)
        {
            writer.WritePropertyName("LoadedMaterialResources");
            serializer.Serialize(writer, LoadedMaterialResources);
        }

        if (CachedExpressionData is not null)
        {
            writer.WritePropertyName("CachedExpressionData");
            serializer.Serialize(writer, CachedExpressionData);
        }

    }

    public override void GetParams(CMaterialParams parameters)
    {
        if (FlattenedTexture != null) parameters.Diffuse = FlattenedTexture;
        if (MobileBaseTexture != null) parameters.Diffuse = MobileBaseTexture;
        if (MobileNormalTexture != null) parameters.Normal = MobileNormalTexture;
        if (MobileMaskTexture != null) parameters.Opacity = MobileMaskTexture;
        parameters.UseMobileSpecular = bUseMobileSpecular;
        parameters.MobileSpecularPower = MobileSpecularPower;
        parameters.MobileSpecularMask = MobileSpecularMask;
    }

    public override void GetParams(CMaterialParams2 parameters, EMaterialFormat format)
    {
        for (int i = 0; i < TextureStreamingData.Length; i++)
        {
            var name = TextureStreamingData[i].TextureName.Text;
            if (!parameters.TryGetTexture2d(out var texture, name))
                continue;

            parameters.VerifyTexture(name, texture, false);
        }

        // *****************************************
        // CachedExpressionData ONLY AFTER THIS LINE
        // *****************************************

        if (CachedExpressionData == null ||
            !CachedExpressionData.TryGetValue(out FStructFallback materialParameters, "Parameters") ||
            !materialParameters.TryGetAllValues(out FStructFallback[] runtimeEntries, "RuntimeEntries"))
            return;

        if (materialParameters.TryGetValue(out float[] scalarValues, "ScalarValues") &&
            runtimeEntries[0].TryGetValue(out FMaterialParameterInfo[] scalarParameterInfos, "ParameterInfos"))
            for (int i = 0; i < scalarParameterInfos.Length; i++)
                parameters.Scalars[scalarParameterInfos[i].Name.Text] = scalarValues[i];

        if (materialParameters.TryGetValue(out FLinearColor[] vectorValues, "VectorValues") &&
            runtimeEntries[1].TryGetValue(out FMaterialParameterInfo[] vectorParameterInfos, "ParameterInfos"))
            for (int i = 0; i < vectorParameterInfos.Length; i++)
                parameters.Colors[vectorParameterInfos[i].Name.Text] = vectorValues[i];

        if (materialParameters.TryGetValue(out FPackageIndex[] textureValues, "TextureValues") &&
            runtimeEntries[2].TryGetValue(out FMaterialParameterInfo[] textureParameterInfos, "ParameterInfos"))
        {
            for (int i = 0; i < textureParameterInfos.Length; i++)
            {
                var name = textureParameterInfos[i].Name.Text;
                UTexture? texture = null;
                if (textureValues[i].TryLoad<UTexture>(out texture) && texture != null)
                {
                    parameters.VerifyTexture(name, texture);
                }
            }
        }
    }

    public void DeserializeInlineShaderMaps(FAssetArchive Ar, ICollection<FMaterialResource> loadedResources)
    {
        var numLoadedResources = Ar.Read<int>();
        if (numLoadedResources > 0)
        {
            FMaterialResourceProxyReader resourceAr;
            if (Ar.Game != EGame.GAME_潜行者2_切尔诺贝利之心)
            {
                resourceAr = new FMaterialResourceProxyReader(Ar);
            }
            else
            {
                var ShaderMaps = new FByteBulkData(Ar);
                if (ShaderMaps.Data == null)
                {
                    throw new InvalidOperationException("ShaderMaps.Data为空,无法创建FByteArchive.");
                }
                var ShaderMapsAr = new FByteArchive("ShaderMaps", ShaderMaps.Data, Ar.Versions);
                resourceAr = new FMaterialResourceProxyReader(ShaderMapsAr);
            }

            for (var resourceIndex = 0; resourceIndex < numLoadedResources; ++resourceIndex)
            {
                var loadedResource = new FMaterialResource();
                loadedResource.DeserializeInlineShaderMap(resourceAr);
                loadedResources.Add(loadedResource);
            }
        }
    }
}