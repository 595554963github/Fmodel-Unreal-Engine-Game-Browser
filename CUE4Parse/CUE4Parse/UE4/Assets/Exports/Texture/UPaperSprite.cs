using System;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.Texture;

public class UPaperSprite : UObject
{
    public FVector2D BakedSourceUV { get; private set; }
    public FVector2D BakedSourceDimension { get; private set; }
    public FPackageIndex? BakedSourceTexture { get; private set; }
    public FPackageIndex? DefaultMaterial { get; private set; }
    public float PixelsPerUnrealUnit { get; private set; }
    public FVector4[] BakedRenderData { get; private set; }

    public UPaperSprite()
    {
        BakedSourceUV = FVector2D.ZeroVector;
        BakedSourceDimension = FVector2D.ZeroVector;
        BakedSourceTexture = null;
        DefaultMaterial = null;
        PixelsPerUnrealUnit = 1f;
        BakedRenderData = Array.Empty<FVector4>();
    }

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        BakedSourceUV = GetOrDefault(nameof(BakedSourceUV), FVector2D.ZeroVector);
        BakedSourceDimension = GetOrDefault(nameof(BakedSourceDimension), FVector2D.ZeroVector);
        BakedSourceTexture = GetOrDefault<FPackageIndex>(nameof(BakedSourceTexture));
        DefaultMaterial = GetOrDefault<FPackageIndex>(nameof(DefaultMaterial));
        PixelsPerUnrealUnit = GetOrDefault(nameof(PixelsPerUnrealUnit), 1f);
        BakedRenderData = GetOrDefault(nameof(BakedRenderData), Array.Empty<FVector4>());
    }
}