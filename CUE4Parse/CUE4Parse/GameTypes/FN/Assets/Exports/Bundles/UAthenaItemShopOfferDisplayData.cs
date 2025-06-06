using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Engine;
using System;
namespace CUE4Parse.GameTypes.FN.Assets.Exports.Bundles;

public class UAthenaItemShopOfferDisplayData : UPrimaryDataAsset
{
    public required FContextualPresentation[] ContextualPresentations;
    public FThreeDPreviewOverrideData[] OverridePreviews;
    public UAthenaItemShopOfferDisplayData()
    {
        OverridePreviews = Array.Empty<FThreeDPreviewOverrideData>();
    }

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        ContextualPresentations = GetOrDefault<FContextualPresentation[]>(nameof(ContextualPresentations), []);
        OverridePreviews = GetOrDefault<FThreeDPreviewOverrideData[]>(nameof(OverridePreviews), []);
    }
}