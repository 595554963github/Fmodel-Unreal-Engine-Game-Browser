using System;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Objects.Meshes;

[JsonConverter(typeof(FPositionVertexBufferConverter))]
public class FPositionVertexBuffer
{
    public FVector[] Verts;
    public int Stride;
    public int NumVertices;

    public FPositionVertexBuffer()
    {
        Verts = [];
    }

    public FPositionVertexBuffer(FArchive Ar)
    {
        if (Ar.Game is EGame.GAME_黎明觉醒 or EGame.GAME_巅峰极速)
        {
            bool bUseFullPrecisionPositions = Ar.Game == EGame.GAME_黎明觉醒 && Ar.ReadBoolean();
            Stride = Ar.Read<int>();
            NumVertices = Ar.Read<int>();
            bUseFullPrecisionPositions = Ar.Game == EGame.GAME_巅峰极速 && Stride == 12;
            Verts = bUseFullPrecisionPositions ? Ar.ReadBulkArray<FVector>() : Ar.ReadBulkArray<FVector>(() => Ar.Read<FVector3UnsignedShort>());
            return;
        }
        Stride = Ar.Read<int>();
        NumVertices = Ar.Read<int>();
        if (Ar.Game == EGame.GAME_无畏契约)
        {
            bool bUseFullPrecisionPositions = Ar.ReadBoolean();
            var bounds = new FBoxSphereBounds(Ar);
            if (!bUseFullPrecisionPositions)
            {
                var vertsHalf = Ar.ReadBulkArray<FVector3SignedShortScale>();
                Verts = new FVector[vertsHalf.Length];
                for (int i = 0; i < vertsHalf.Length; i++)
                    Verts[i] = vertsHalf[i] * bounds.BoxExtent + bounds.Origin;
                return;
            }
        }
        if (Ar.Game is EGame.GAME_哥特王朝重制版 && Stride == 8)
        {
            var vertsHalf = Ar.ReadBulkArray<FHalfVector4>();
            Verts = new FVector[vertsHalf.Length];
            for (int i = 0; i < vertsHalf.Length; i++)
                Verts[i] = vertsHalf[i];
            return;
        }
        if (Ar.Game is EGame.GAME_往日不再)
        {
            Verts = Stride switch
            {
                4 => Ar.ReadBulkArray(() => (FVector) Ar.Read<FVector3Packed32>()),
                8 => Ar.ReadBulkArray(() => (FVector) Ar.Read<FVector3UnsignedShortScale>()),
                12 => Ar.ReadBulkArray<FVector>(),
                _ => throw new ArgumentOutOfRangeException($"未知的步幅{Stride} for FPositionVertexBuffer")
            };
            return;
        }
        if (Ar.Game == EGame.GAME_指环王_中土之战) Ar.Position += 25;
        Verts = Ar.ReadBulkArray<FVector>();
    }
}