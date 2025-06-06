using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.AssetRegistry.Readers;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.AssetRegistry.Objects
{
    public class FNameTableArchiveReader : FAssetRegistryArchive
    {
        public FNameTableArchiveReader(FArchive Ar, FAssetRegistryHeader header) : base(Ar, header)
        {
            var nameOffset = Ar.Read<long>();
            if (nameOffset > Ar.Length)
                throw new ArgumentOutOfRangeException(nameof(nameOffset), "档案已损坏");

            if (nameOffset > 0)
            {
                var originalOffset = Ar.Position;
                Ar.Position = nameOffset;

                var nameCount = Ar.Read<int>();
                if (nameCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(nameCount), "档案已损坏");

                var maxReservation = (Ar.Length - Ar.Position) / sizeof(int);
                NameMap = new FNameEntrySerialized[Math.Min(nameCount, maxReservation)];
                Ar.ReadArray(NameMap, () => new FNameEntrySerialized(Ar));

                Ar.Position = originalOffset;
            }
            else
            {
                NameMap = Array.Empty<FNameEntrySerialized>();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override FName ReadFName()
        {
            var nameIndex = baseArchive.Read<int>();
            var number = baseArchive.Read<int>();
#if !NO_FNAME_VALIDATION
            if (nameIndex < 0 || nameIndex >= NameMap.Length)
            {
                throw new ParserException(baseArchive, $"无法读取FName,请求索引{nameIndex},名称映射大小{NameMap.Length}");
            }
#endif
            return new FName(NameMap[nameIndex], nameIndex, number);
        }

        public override void SerializeTagsAndBundles(FAssetData assetData)
        {
            var size = baseArchive.Read<int>();
            var ret = new Dictionary<FName, string>();
            for (var i = 0; i < size; i++)
            {
                ret[ReadFName()] = baseArchive.ReadFString();
            }
            assetData.TagsAndValues = ret;
            assetData.TaggedAssetBundles = new FAssetBundleData();
        }

        public override object Clone() => new FNameTableArchiveReader((FArchive) baseArchive.Clone(), Header);
    }
}