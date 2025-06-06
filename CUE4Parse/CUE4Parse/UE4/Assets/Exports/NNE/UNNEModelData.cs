using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Assets.Exports.NNE;

public class UNNEModelData : UObject
{
    public string[] TargetRuntimes { get; private set; } = Array.Empty<string>();
    public string FileType { get; private set; } = string.Empty;
    public byte[] FileData { get; private set; } = Array.Empty<byte>();
    public Dictionary<string, byte[]> AdditionalFileData { get; private set; } = new();
    public FGuid FileId { get; private set; }
    public Dictionary<string, byte[]> ModelData { get; private set; } = new();

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        switch (NNEModelDataVersion.Get(Ar))
        {
            case NNEModelDataVersion.Type.V0:
            case NNEModelDataVersion.Type.V1:
                FileType = Ar.ReadFString();
                FileData = Ar.ReadArray<byte>();
                FileId = Ar.Read<FGuid>();
                ModelData = Ar.ReadMap(Ar.ReadFString, Ar.ReadArray<byte>);
                break;
            case NNEModelDataVersion.Type.V2:
                TargetRuntimes = Ar.ReadArray(Ar.ReadFString);
                FileType = Ar.ReadFString();
                FileData = Ar.ReadArray<byte>();
                FileId = Ar.Read<FGuid>();
                var numItems = Ar.Read<int>();
                ModelData = new Dictionary<string, byte[]>();
                for (var i = 0; i < numItems; i++)
                {
                    var name = Ar.ReadFString();
                    var memoryAlignment = Ar.Read<int>();
                    var dataSize = Ar.Read<int>();
                    ModelData[name] = Ar.ReadArray<byte>(dataSize);
                }
                break;
            case NNEModelDataVersion.Type.V3:
                TargetRuntimes = Ar.ReadArray(Ar.ReadFString);
                FileType = Ar.ReadFString();
                FileData = Ar.ReadArray<byte>();
                AdditionalFileData = Ar.ReadMap(Ar.ReadFString, Ar.ReadArray<byte>);
                FileId = Ar.Read<FGuid>();
                numItems = Ar.Read<int>();
                ModelData = new Dictionary<string, byte[]>();
                for (var i = 0; i < numItems; i++)
                {
                    var name = Ar.ReadFString();
                    var memoryAlignment = Ar.Read<uint>();
                    var dataSize = Ar.Read<ulong>();
                    ModelData[name] = Ar.ReadArray<byte>((int)dataSize);
                }
                break;
            case NNEModelDataVersion.Type.V4:
                TargetRuntimes = Ar.ReadArray(Ar.ReadFString);
                FileType = Ar.ReadFString();
                FileData = Ar.ReadArray<byte>((int)Ar.Read<ulong>());
                AdditionalFileData = Ar.ReadMap(Ar.ReadFString, () => Ar.ReadArray<byte>((int)Ar.Read<ulong>()));
                FileId = Ar.Read<FGuid>();
                numItems = Ar.Read<int>();
                ModelData = new Dictionary<string, byte[]>();
                for (var i = 0; i < numItems; i++)
                {
                    var name = Ar.ReadFString();
                    var memoryAlignment = Ar.Read<uint>();
                    var dataSize = Ar.Read<ulong>();
                    ModelData[name] = Ar.ReadArray<byte>((int)dataSize);
                }
                break;
        }
    }
}

public static class NNEModelDataVersion
{
    public enum Type
    {
        V0 = 0,
        V1 = 1,
        V2 = 2,
        V3 = 3,
        V4 = 4,
        VersionPlusOne,
        LatestVersion = VersionPlusOne - 1
    }

    public static readonly FGuid GUID = new(0x9513202E, 0xEBA1B279, 0xF17FE5BA, 0xAB90C3F2);

    public static Type Get(FArchive Ar)
    {
        var ver = Ar.CustomVer(GUID);
        if (ver >= 0)
            return (Type)ver;

        return Ar.Game switch
        {
            < EGame.GAME_UE5_2 => (Type)(-1),
            < EGame.GAME_UE5_3 => Type.V0,
            < EGame.GAME_UE5_4 => Type.V1,
            < EGame.GAME_UE5_5 => Type.V3,
            < EGame.GAME_UE5_6 => Type.V4,
            _ => Type.LatestVersion
        };
    }
}