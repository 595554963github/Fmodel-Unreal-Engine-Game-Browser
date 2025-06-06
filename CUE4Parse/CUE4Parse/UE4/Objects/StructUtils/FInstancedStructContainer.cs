using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using System;
namespace CUE4Parse.UE4.Objects.StructUtils
{
    public struct FInstancedStructContainer : IUStruct
    {
        public FStructFallback[] Structs;

        public FInstancedStructContainer(FAssetArchive Ar)
        {
            var version = Ar.Read<EVersion>();

            FPackageIndex[] Items = Ar.ReadArray(() => new FPackageIndex(Ar)) ?? Array.Empty<FPackageIndex>();
            Structs = new FStructFallback[Items.Length]; 

            for (int index = 0; index < Items.Length; index++)
            {
                var item = Items[index];
                var size = Ar.Read<int>();
                var savedPos = Ar.Position;
                FStructFallback strukt;

                if (item.TryLoad<UStruct>(out var NonConstStruct))
                {
                    try
                    {
                        strukt = new FStructFallback(Ar, NonConstStruct);
                    }
                    catch
                    {
                        Ar.Position = savedPos + size;
                        strukt = new FStructFallback(); // ��ʼ����ʵ������ null
                    }
                }
                else
                {
                    Ar.Position += size;
                    strukt = new FStructFallback(); // ��ʼ����ʵ������ null
                }

                Structs[index] = strukt; // ȷ���ǿո�ֵ
            }
        }

        enum EVersion : byte
        {
            InitialVersion = 0,
            VersionPlusOne,
            LatestVersion = VersionPlusOne - 1
        }
    }
}