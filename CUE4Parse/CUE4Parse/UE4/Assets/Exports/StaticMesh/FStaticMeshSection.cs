using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.StaticMesh;

[JsonConverter(typeof(FStaticMeshSectionConverter))]
public class FStaticMeshSection
{
    public int MaterialIndex;
    public int FirstIndex;
    public int NumTriangles;
    public int MinVertexIndex;
    public int MaxVertexIndex;
    public bool bEnableCollision;
    public bool bCastShadow;
    public bool bForceOpaque;
    public bool bVisibleInRayTracing;
    public bool bAffectDistanceFieldLighting;
    public int CustomData;

    public FStaticMeshSection(FArchive Ar)
    {
        MaterialIndex = Ar.Read<int>();
        FirstIndex = Ar.Read<int>();
        NumTriangles = Ar.Read<int>();
        MinVertexIndex = Ar.Read<int>();
        MaxVertexIndex = Ar.Read<int>();
        bEnableCollision = Ar.ReadBoolean();
        bCastShadow = Ar.ReadBoolean();
        if (Ar.Game == EGame.GAME_������������ɱ) Ar.Position += 5; // byte + int
        bForceOpaque = FRenderingObjectVersion.Get(Ar) >= FRenderingObjectVersion.Type.StaticMeshSectionForceOpaqueField && Ar.ReadBoolean();
        if (Ar.Game == EGame.GAME_���˿��1) Ar.Position += 8; // "None" FName
        bVisibleInRayTracing = !Ar.Versions["StaticMesh.HasVisibleInRayTracing"] || Ar.ReadBoolean();
        if (Ar.Game is EGame.GAME_��������) Ar.Position += 8;
        bAffectDistanceFieldLighting = Ar.Game >= EGame.GAME_UE5_1 && Ar.ReadBoolean();
        if (Ar.Game is EGame.GAME_������˾ or EGame.GAME_�������� or EGame.GAME_�۷弫�� or EGame.GAME_�������� or EGame.GAME_����) Ar.Position += 4;
        if (Ar.Game is EGame.GAME_����ůů)
        {
            CustomData = Ar.Read<int>();
            Ar.Position += 8;
        }
    }
}