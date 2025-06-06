using System;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.RenderCore;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.StaticMesh;

[JsonConverter(typeof(FStaticMeshVertexBufferConverter))]
public class FStaticMeshVertexBuffer
{
    public int NumTexCoords;
    public int Strides;
    public int NumVertices;
    public bool UseFullPrecisionUVs;
    public bool UseHighPrecisionTangentBasis;
    public FStaticMeshUVItem[] UV;  // TangentsData ?

    public FStaticMeshVertexBuffer()
    {
        UV = [];
    }

    public FStaticMeshVertexBuffer(FArchive Ar)
    {
        var stripDataFlags = new FStripDataFlags(Ar, FPackageFileVersion.CreateUE4Version(EUnrealEngineObjectUE4Version.STATIC_SKELETAL_MESH_SERIALIZATION_FIX));

        NumTexCoords = Ar.Read<int>();
        Strides = Ar.Game < EGame.GAME_UE4_19 ? Ar.Read<int>() : -1;
        NumVertices = Ar.Read<int>();
        UseFullPrecisionUVs = Ar.ReadBoolean();
        UseHighPrecisionTangentBasis = Ar.Game >= EGame.GAME_UE4_12 && Ar.ReadBoolean();
        if (Ar.Game == EGame.GAME_三角洲行动) Ar.Position += 4;

        if (!stripDataFlags.IsAudioVisualDataStripped())
        {
            if (Ar.Game < EGame.GAME_UE4_19)
            {
                UV = Ar.ReadBulkArray(() => new FStaticMeshUVItem(Ar, UseHighPrecisionTangentBasis, NumTexCoords, UseFullPrecisionUVs));
            }
            else
            {
                var tempTangents = Array.Empty<FPackedNormal[]>();
                if (Ar.Game is EGame.GAME_星球大战绝地_陨落的武士团 or EGame.GAME_星球大战绝地_幸存者 && Ar.ReadBoolean()) // bDropNormals
                {
                    goto texture_coordinates;
                }
                var itemSize = Ar.Read<int>();
                var itemCount = Ar.Read<int>();
                var position = Ar.Position;

                if (itemCount != NumVertices)
                    throw new ParserException($"顶点数量不匹配:读取={itemCount} != 预期={NumVertices}");

                tempTangents = Ar.ReadArray(NumVertices, () => FStaticMeshUVItem.SerializeTangents(Ar, UseHighPrecisionTangentBasis));
                if (Ar.Position - position != itemCount * itemSize)
                    throw new ParserException($"读取的切线数据字节数不正确，当前位置:{Ar.Position}，应读取:{position + itemSize * itemCount}，差值: {position + (itemSize * itemCount) - Ar.Position}");

                texture_coordinates:
                itemSize = Ar.Read<int>();
                itemCount = Ar.Read<int>();
                position = Ar.Position;

                if (itemCount != NumVertices * NumTexCoords)
                    throw new ParserException($"顶点数量不匹配:读取={itemCount}!= 预期={NumVertices * NumTexCoords}");

                var uv = Ar.ReadArray(NumVertices, () => FStaticMeshUVItem.SerializeTexcoords(Ar, NumTexCoords, UseFullPrecisionUVs));
                if (Ar.Position - position != itemCount * itemSize)
                    throw new ParserException($"读取的纹理坐标数据字节数不正确，当前位置:{Ar.Position}，应读取:{position + itemSize * itemCount}，差值: {position + (itemSize * itemCount) - Ar.Position}");

                UV = new FStaticMeshUVItem[NumVertices];
                for (var i = 0; i < NumVertices; i++)
                {
                    if (Ar.Game is EGame.GAME_星球大战绝地_陨落的武士团 or EGame.GAME_星球大战绝地_幸存者 && tempTangents.Length == 0)
                    {
                        UV[i] = new FStaticMeshUVItem([new FPackedNormal(0), new FPackedNormal(0), new FPackedNormal(0)], uv[i]);
                    }
                    else
                    {
                        UV[i] = new FStaticMeshUVItem(tempTangents[i], uv[i]);
                    }
                }

                if (Ar.Game == EGame.GAME_火炬之光_无限) Ar.SkipBulkArrayData();
            }
        }
        else
        {
            UV = [];
        }
    }
}