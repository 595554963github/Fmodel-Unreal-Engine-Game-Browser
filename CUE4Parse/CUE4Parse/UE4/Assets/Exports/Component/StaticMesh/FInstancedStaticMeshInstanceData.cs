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
            EGame.GAME_�����ִ��Ų� => Ar.Read<int>() * sizeof(int),
            EGame.GAME_�ӳ����� or EGame.GAME_������������ɱ or EGame.GAME_����֮��
                or EGame.GAME_���ղ��� or EGame.GAME_����ůů => 16,
            EGame.GAME_�ž���2���ư� or EGame.GAME_���ù���2 => 32,
            _ => 0,
        };
        TransformData.SetFromMatrix(Transform);
    }

    public override string ToString()
    {
        return TransformData.ToString();
    }
}