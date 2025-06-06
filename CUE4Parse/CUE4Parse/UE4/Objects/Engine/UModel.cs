using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.RenderCore;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System;
namespace CUE4Parse.UE4.Objects.Engine
{
    public readonly struct FVert : IUStruct
    {
        public readonly int pVertex;
        public readonly int iSide;
        public readonly FVector2D ShadowTexCoord;
        public readonly FVector2D BackfaceShadowTexCoord;

        public FVert(FAssetArchive Ar)
        {
            pVertex = Ar.Read<int>();
            iSide = Ar.Read<int>();
            ShadowTexCoord = new FVector2D(Ar);
            BackfaceShadowTexCoord = new FVector2D(Ar);
        }
    }

    public enum EBspNodeFlags : byte
    {
        NF_NotCsg = 0x01,
        NF_NotVisBlocking = 0x04,
        NF_BrightCorners = 0x10,
        NF_IsNew = 0x20,
        NF_IsFront = 0x40,
        NF_IsBack = 0x80,
    }

    public readonly struct FBspNode : IUStruct
    {
        public const int MAX_NODE_VERTICES = 255;
        public const int MAX_ZONES = 64;

        public readonly FPlane Plane;
        public readonly int iVertPool;
        public readonly int iSurf;
        public readonly int iVertexIndex;
        public readonly ushort ComponentIndex;
        public readonly ushort ComponentNodeIndex;
        public readonly int ComponentElementIndex;
        public readonly int iBack;
        public readonly int iFront;
        public readonly int iPlane;
        public readonly int iCollisionBound;
        public readonly byte iZone0;
        public readonly byte iZone1;
        public readonly byte NumVertices;
        public readonly EBspNodeFlags NodeFlags;
        public readonly int iLeaf0;
        public readonly int iLeaf1;

        public FBspNode(FAssetArchive Ar)
        {
            Plane = Ar.Read<FPlane>();
            iVertPool = Ar.Read<int>();
            iSurf = Ar.Read<int>();
            iVertexIndex = Ar.Read<int>();
            ComponentIndex = Ar.Read<ushort>();
            ComponentNodeIndex = Ar.Read<ushort>();
            ComponentElementIndex = Ar.Read<int>();
            iBack = Ar.Read<int>();
            iFront = Ar.Read<int>();
            iPlane = Ar.Read<int>();
            iCollisionBound = Ar.Read<int>();
            iZone0 = Ar.Read<byte>();
            iZone1 = Ar.Read<byte>();
            NumVertices = Ar.Read<byte>();
            NodeFlags = Ar.Read<EBspNodeFlags>();
            iLeaf0 = Ar.Read<int>();
            iLeaf1 = Ar.Read<int>();
        }
    }

    public readonly struct FZoneSet : IUStruct
    {
        public readonly ulong MaskBits;

        public FZoneSet(FAssetArchive Ar)
        {
            MaskBits = Ar.Read<ulong>();
        }
    }

    public readonly struct FZoneProperties : IUStruct
    {
        public readonly FPackageIndex ZoneActor;
        public readonly float LastRenderTime;
        public readonly FZoneSet Connectivity;
        public readonly FZoneSet Visibility;

        public FZoneProperties(FAssetArchive Ar)
        {
            ZoneActor = new FPackageIndex(Ar);
            Connectivity = new FZoneSet(Ar);
            Visibility = new FZoneSet(Ar);
            LastRenderTime = Ar.Read<float>();
        }
    }

    public readonly struct FBspSurf : IUStruct
    {
        public readonly FPackageIndex Material;
        public readonly uint PolyFlags;
        public readonly int pBase;
        public readonly int vNormal;
        public readonly int vTextureU;
        public readonly int vTextureV;
        public readonly int iBrushPoly;
        public readonly FPackageIndex Actor;
        public readonly FPlane Plane;
        public readonly float LightMapScale;
        public readonly int iLightmassIndex;

        public FBspSurf(FAssetArchive Ar)
        {
            Material = new FPackageIndex(Ar);
            PolyFlags = Ar.Read<uint>();
            pBase = Ar.Read<int>();
            vNormal = Ar.Read<int>();
            vTextureU = Ar.Read<int>();
            vTextureV = Ar.Read<int>();
            iBrushPoly = Ar.Read<int>();
            Actor = new FPackageIndex(Ar);
            Plane = Ar.Read<FPlane>();
            LightMapScale = Ar.Read<float>();
            iLightmassIndex = Ar.Read<int>();
        }
    }

    public struct FModelVertex : IUStruct
    {
        public FVector Position;
        public FVector TangentX;
        public FVector4 TangentZ;
        public FVector2D TexCoord;
        public FVector2D ShadowTexCoord;

        public FModelVertex(FArchive Ar)
        {
            Position = Ar.Read<FVector>();

            if (FRenderingObjectVersion.Get(Ar) < FRenderingObjectVersion.Type.IncreaseNormalPrecision)
            {
                TangentX = (FVector)Ar.Read<FDeprecatedSerializedPackedNormal>();
                TangentZ = (FVector4)Ar.Read<FDeprecatedSerializedPackedNormal>();
            }
            else
            {
                TangentX = Ar.Read<FVector>();
                TangentZ = Ar.Read<FVector4>();
            }

            TexCoord = Ar.Read<FVector2D>();
            ShadowTexCoord = Ar.Read<FVector2D>();
        }

        public FVector GetTangentY() => ((FVector)TangentZ ^ TangentX) * TangentZ.W;
    }

    public struct FDeprecatedModelVertex : IUStruct
    {
        public FVector Position;
        public FDeprecatedSerializedPackedNormal TangentX;
        public FDeprecatedSerializedPackedNormal TangentZ;
        public FVector2D TexCoord;
        public FVector2D ShadowTexCoord;

        public FDeprecatedModelVertex(FArchive Ar)
        {
            Position = Ar.Read<FVector>();
            TangentX = Ar.Read<FDeprecatedSerializedPackedNormal>();
            TangentZ = Ar.Read<FDeprecatedSerializedPackedNormal>();
            TexCoord = Ar.Read<FVector2D>();
            ShadowTexCoord = Ar.Read<FVector2D>();
        }

        public static implicit operator FModelVertex(FDeprecatedModelVertex v) => new()
        {
            Position = v.Position,
            TangentX = (FVector)v.TangentX,
            TangentZ = (FVector4)v.TangentZ,
            TexCoord = v.TexCoord,
            ShadowTexCoord = v.ShadowTexCoord
        };
    }

    public class FModelVertexBuffer : IUStruct
    {
        public readonly FModelVertex[] Vertices;

        public FModelVertexBuffer(FArchive Ar)
        {
            if (FRenderingObjectVersion.Get(Ar) < FRenderingObjectVersion.Type.IncreaseNormalPrecision)
            {
                var deprecatedVertices = Ar.ReadBulkArray<FDeprecatedModelVertex>();
                Vertices = new FModelVertex[deprecatedVertices.Length];
                for (int i = 0; i < Vertices.Length; i++)
                {
                    Vertices[i] = deprecatedVertices[i];
                }
            }
            else
            {
                Vertices = Ar.ReadArray(() => new FModelVertex(Ar));
            }
        }
    }

    public class UModel : Assets.Exports.UObject
    {
        public FBoxSphereBounds Bounds { get; private set; } = new FBoxSphereBounds();
        public FVector[] Vectors { get; private set; } = Array.Empty<FVector>();
        public FVector[] Points { get; private set; } = Array.Empty<FVector>();
        public FBspNode[] Nodes { get; private set; } = Array.Empty<FBspNode>();
        public FBspSurf[] Surfs { get; private set; } = Array.Empty<FBspSurf>();
        public FVert[] Verts { get; private set; } = Array.Empty<FVert>();
        public int NumSharedSides { get; private set; }
        public bool RootOutside { get; private set; }
        public bool Linked { get; private set; }
        public uint NumUniqueVertices { get; private set; }
        public FModelVertexBuffer? VertexBuffer { get; private set; }
        public FGuid LightingGuid { get; private set; }
        public FLightmassPrimitiveSettings[] LightmassSettings { get; private set; } = Array.Empty<FLightmassPrimitiveSettings>();

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            const int StripVertexBufferFlag = 1;
            var stripData = new FStripDataFlags(Ar);

            Bounds = new FBoxSphereBounds(Ar);

            Vectors = Ar.ReadBulkArray<FVector>() ?? Array.Empty<FVector>();
            Points = Ar.ReadBulkArray<FVector>() ?? Array.Empty<FVector>();
            Nodes = Ar.ReadBulkArray<FBspNode>() ?? Array.Empty<FBspNode>();

            if (Ar.Ver < EUnrealEngineObjectUE4Version.BSP_UNDO_FIX)
            {
                var surfsOwner = new FPackageIndex(Ar);
                Surfs = Ar.ReadArray(() => new FBspSurf(Ar)) ?? Array.Empty<FBspSurf>();
            }
            else
            {
                Surfs = Ar.ReadArray(() => new FBspSurf(Ar)) ?? Array.Empty<FBspSurf>();
            }
            Verts = Ar.ReadBulkArray<FVert>() ?? Array.Empty<FVert>();

            NumSharedSides = Ar.Read<int>();
            if (Ar.Ver < EUnrealEngineObjectUE4Version.REMOVE_ZONES_FROM_MODEL)
            {
                var dummyZones = Ar.ReadArray<FZoneProperties>();
            }

            var bHasEditorOnlyData = !Ar.IsFilterEditorOnly || Ar.Ver < EUnrealEngineObjectUE4Version.REMOVE_UNUSED_UPOLYS_FROM_UMODEL;
            if (bHasEditorOnlyData)
            {
                var dummyPolys = new FPackageIndex(Ar);
                Ar.SkipBulkArrayData(); // DummyLeafHulls
                Ar.SkipBulkArrayData(); // DummyLeaves
            }

            RootOutside = Ar.ReadBoolean();
            Linked = Ar.ReadBoolean();

            if (Ar.Ver < EUnrealEngineObjectUE4Version.REMOVE_ZONES_FROM_MODEL)
            {
                var dummyPortalNodes = Ar.ReadBulkArray<int>();
            }

            NumUniqueVertices = Ar.Read<uint>();

            if (!stripData.IsEditorDataStripped() || !stripData.IsClassDataStripped(StripVertexBufferFlag))
            {
                VertexBuffer = new FModelVertexBuffer(Ar);
            }

            LightingGuid = Ar.Read<FGuid>();
            LightmassSettings = Ar.ReadArray(() => new FLightmassPrimitiveSettings(Ar)) ?? Array.Empty<FLightmassPrimitiveSettings>();
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName("Bounds");
            serializer.Serialize(writer, Bounds);

            writer.WritePropertyName("Vectors");
            serializer.Serialize(writer, Vectors);

            writer.WritePropertyName("Points");
            serializer.Serialize(writer, Points);

            writer.WritePropertyName("Nodes");
            serializer.Serialize(writer, Nodes);

            writer.WritePropertyName("Surfs");
            serializer.Serialize(writer, Surfs);

            writer.WritePropertyName("Verts");
            serializer.Serialize(writer, Verts);

            writer.WritePropertyName("NumSharedSides");
            serializer.Serialize(writer, NumSharedSides);

            writer.WritePropertyName("RootOutside");
            serializer.Serialize(writer, RootOutside);

            writer.WritePropertyName("Linked");
            serializer.Serialize(writer, Linked);

            writer.WritePropertyName("NumUniqueVertices");
            serializer.Serialize(writer, NumUniqueVertices);

            if (VertexBuffer != null)
            {
                writer.WritePropertyName("VertexBuffer");
                serializer.Serialize(writer, VertexBuffer);
            }

            writer.WritePropertyName("LightingGuid");
            serializer.Serialize(writer, LightingGuid);

            writer.WritePropertyName("LightmassSettings");
            serializer.Serialize(writer, LightmassSettings);
        }
    }
}