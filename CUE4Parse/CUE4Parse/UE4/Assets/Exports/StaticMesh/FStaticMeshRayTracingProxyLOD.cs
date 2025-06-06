using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using System;

namespace CUE4Parse.UE4.Assets.Exports.StaticMesh;

public class FStaticMeshRayTracingProxyLOD
{
    public bool bBuffersInlined = false;
    public bool bOwnsBuffers;
    public bool bOwnsRayTracingGeometry;

    public FStaticMeshSection[] Sections { get; private set; }
    public FPositionVertexBuffer PositionVertexBuffer { get; private set; }
    public FStaticMeshVertexBuffer VertexBuffer { get; private set; }
    public FColorVertexBuffer ColorVertexBuffer { get; private set; }
    public FRawStaticIndexBuffer IndexBuffer { get; private set; }
    public FByteBulkData StreamableData { get; private set; }

    public FStaticMeshRayTracingProxyLOD(FAssetArchive Ar)
    {
        // Initialize with empty/default values first
        Sections = Array.Empty<FStaticMeshSection>();
        PositionVertexBuffer = new FPositionVertexBuffer();
        VertexBuffer = new FStaticMeshVertexBuffer();
        ColorVertexBuffer = new FColorVertexBuffer();
        IndexBuffer = new FRawStaticIndexBuffer();
        StreamableData = new FByteBulkData(Ar);

        bOwnsBuffers = Ar.ReadBoolean();

        if (bOwnsBuffers)
        {
            Sections = Ar.ReadArray(() => new FStaticMeshSection(Ar));
        }

        bOwnsRayTracingGeometry = Ar.ReadBoolean();

        if (Ar.Game >= EGame.GAME_UE5_6)
            bBuffersInlined = Ar.ReadBoolean();

        if (bBuffersInlined)
        {
            SerializeBuffers(Ar);
        }
        else
        {
            StreamableData = new FByteBulkData(Ar);
        }

        if (Ar.Game >= EGame.GAME_UE5_6)
            SerializeMetaData(Ar);
    }

    private void SerializeBuffers(FAssetArchive Ar)
    {
        if (bOwnsBuffers)
        {
            PositionVertexBuffer = new FPositionVertexBuffer(Ar);
            VertexBuffer = new FStaticMeshVertexBuffer(Ar);
            ColorVertexBuffer = new FColorVertexBuffer(Ar);
            IndexBuffer = new FRawStaticIndexBuffer(Ar);
        }

        if (bOwnsRayTracingGeometry)
            Ar.SkipBulkArrayData();
    }

    private void SerializeMetaData(FAssetArchive Ar)
    {
        if (bOwnsBuffers)
        {
            Ar.Position += sizeof(uint); // BuffersSize
            Ar.Position += 2 * sizeof(uint); // PositionVertexBuffer
            Ar.Position += 2 * sizeof(uint) + 2 * sizeof(int); // StaticMeshVertexBuffer
            Ar.Position += 2 * sizeof(uint); // ColorVertexBuffer
            Ar.Position += 2 * sizeof(int); // IndexBuffer
        }

        if (bOwnsRayTracingGeometry)
        {
            Ar.Position += 2 * sizeof(uint); // OfflineBVHOffset + OfflineBVHSize
            _ = Ar.ReadArray<int>(6); // RawDataHeader
        }
    }
}