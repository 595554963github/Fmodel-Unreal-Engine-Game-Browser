using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation.CurveExpression;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using System.Collections.Generic;

public class UCurveExpressionsDataAsset : UObject
{
    public FName[]? NamedConstants;
    public FExpressionData? ExpressionData;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        if (FCurveExpressionCustomVersion.Get(Ar) >= FCurveExpressionCustomVersion.Type.ExpressionDataInSharedObject)
        {
            NamedConstants = Ar.ReadArray(Ar.ReadFName) ?? System.Array.Empty<FName>();
        }
        else
        {
            NamedConstants = System.Array.Empty<FName>();
        }

        ExpressionData = new FExpressionData(Ar);
    }
}

public class FExpressionData
{
    public Dictionary<FName, FExpressionObject> ExpressionMap;

    public FExpressionData(FArchive Ar)
    {
        ExpressionMap = Ar.ReadMap(() => (Ar.ReadFName(), new FExpressionObject(Ar)))
                      ?? new Dictionary<FName, FExpressionObject>();
    }
}