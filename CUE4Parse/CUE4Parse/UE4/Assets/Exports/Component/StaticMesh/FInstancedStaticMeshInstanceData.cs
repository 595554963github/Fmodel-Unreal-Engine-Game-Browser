using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;

public class FInstancedStaticMeshInstanceData
{
    private readonly FMatrix Transform;

    public readonly FTransform TransformData = new();

    public FInstancedStaticMeshInstanceData(FArchive Ar)
    {
        Transform = new FMatrix(Ar);

        Ar.Position += Ar.Game switch
        {
            EGame.GAME_霍格沃茨遗产 => Ar.Read<int>() * sizeof(int),
            EGame.GAME_逃出生天 or EGame.GAME_绝地求生大逃杀 or EGame.GAME_盗贼之海
                or EGame.GAME_往日不再 or EGame.GAME_无限暖暖 => 16,
            EGame.GAME_寂静岭2重制版 or EGame.GAME_腐烂国度2 => 32,
            _ => 0,
        };
        TransformData.SetFromMatrix(Transform);
    }

    public override string ToString()
    {
        return TransformData.ToString();
    }
}