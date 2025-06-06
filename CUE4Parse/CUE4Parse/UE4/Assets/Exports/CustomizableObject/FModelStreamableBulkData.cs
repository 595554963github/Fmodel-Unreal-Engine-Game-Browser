using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

public class FModelStreamableBulkData
{
    public Dictionary<uint, FMutableStreamableBlock> ModelStreamables;
    public Dictionary<uint, FClothingStreamable> ClothingStreamables;
    public Dictionary<uint, FRealTimeMorphStreamable> RealTimeMorphStreamables;
    public FByteBulkData[] StreamableBulkData;

    public FModelStreamableBulkData(FAssetArchive Ar, bool bCooked)
    {
        ModelStreamables = Ar.ReadMap(() => (Ar.Read<uint>(), new FMutableStreamableBlock(Ar)));
        ClothingStreamables = Ar.ReadMap(() => (Ar.Read<uint>(), new FClothingStreamable(Ar)));
        RealTimeMorphStreamables = Ar.ReadMap(() => (Ar.Read<uint>(), new FRealTimeMorphStreamable(Ar)));

        StreamableBulkData = bCooked
            ? ReadCookedBulkData(Ar)
            : new FByteBulkData[0];
    }

    private FByteBulkData[] ReadCookedBulkData(FAssetArchive Ar)
    {
        var count = Ar.Read<int>();
        var data = new FByteBulkData[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = new FByteBulkData(Ar);
        }
        return data;
    }
}