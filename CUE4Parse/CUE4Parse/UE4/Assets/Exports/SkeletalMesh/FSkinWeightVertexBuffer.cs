using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using System;

namespace CUE4Parse.UE4.Assets.Exports.SkeletalMesh;

public class FSkinWeightVertexBuffer
{
    private const int _NUM_INFLUENCES_UE4 = 4;

    public FSkinWeightInfo[] Weights;

    public FSkinWeightVertexBuffer()
    {
        Weights = Array.Empty<FSkinWeightInfo>();
    }

    public FSkinWeightVertexBuffer(FArchive Ar, bool numSkelCondition)
    {
        var bNewWeightFormat = FAnimObjectVersion.Get(Ar) >= FAnimObjectVersion.Type.UnlimitedBoneInfluences;
        var dataStripFlags = Ar.Read<FStripDataFlags>();
        bool bVariableBonesPerVertex = false;
        bool bExtraBoneInfluences;
        uint maxBoneInfluences;
        bool bUse16BitBoneIndex = false;
        uint numVertices;

        if (!Ar.Versions["SkeletalMesh.UseNewCookedFormat"])
        {
            bExtraBoneInfluences = Ar.ReadBoolean();
            numVertices = Ar.Read<uint>();
            maxBoneInfluences = bExtraBoneInfluences ? 8u : 4u;
        }
        else if (!bNewWeightFormat)
        {
            bExtraBoneInfluences = Ar.ReadBoolean();
            if (FSkeletalMeshCustomVersion.Get(Ar) >= FSkeletalMeshCustomVersion.Type.SplitModelAndRenderData)
            {
                Ar.Position += 4; 
            }
            numVertices = Ar.Read<uint>();
            maxBoneInfluences = bExtraBoneInfluences ? 8u : 4u;
            bVariableBonesPerVertex = false;
        }
        else
        {
            bVariableBonesPerVertex = Ar.ReadBoolean();
            maxBoneInfluences = Ar.Read<uint>();
            numVertices = Ar.Read<uint>();
            bExtraBoneInfluences = maxBoneInfluences > _NUM_INFLUENCES_UE4;
            if (FAnimObjectVersion.Get(Ar) >= FAnimObjectVersion.Type.IncreaseBoneIndexLimitPerChunk)
            {
                bUse16BitBoneIndex = Ar.ReadBoolean();
            }
        }

        byte[] newData = Array.Empty<byte>();
        if (!dataStripFlags.IsAudioVisualDataStripped())
        {
            if (!bNewWeightFormat)
            {
                Weights = Ar.ReadBulkArray(() => new FSkinWeightInfo(Ar, bExtraBoneInfluences, bUse16BitBoneIndex));
                return;
            }
            newData = Ar.ReadBulkArray<byte>();
        }
        else
        {
            bExtraBoneInfluences = numSkelCondition;
        }

        if (bNewWeightFormat)
        {
            var lookupStripFlags = Ar.Read<FStripDataFlags>();
            var numLookupVertices = Ar.Read<int>();
            uint[] lookupData = Array.Empty<uint>();
            if (!lookupStripFlags.IsAudioVisualDataStripped())
            {
                lookupData = Ar.ReadBulkArray<uint>();
            }

            if (newData.Length > 0)
            {
                using var tempAr = new FByteArchive("WeightsReader", newData, Ar.Versions);
                Weights = new FSkinWeightInfo[numVertices];

                if (bVariableBonesPerVertex)
                {
                    if (lookupData.Length != numVertices)
                        throw new ParserException($"LookupData长度({lookupData.Length})与顶点数({numVertices})不匹配");

                    for (var i = 0; i < numVertices; i++)
                    {
                        var influenceCount = (byte)(lookupData[i] & 0xFF);
                        var offset = lookupData[i] >> 8;
                        tempAr.Position = offset;
                        Weights[i] = new FSkinWeightInfo(tempAr, bExtraBoneInfluences, bUse16BitBoneIndex, influenceCount);
                    }
                }
                else
                {
                    for (var i = 0; i < numVertices; i++)
                    {
                        Weights[i] = new FSkinWeightInfo(tempAr, bExtraBoneInfluences, bUse16BitBoneIndex);
                    }
                }
            }
        }

        Weights ??= Array.Empty<FSkinWeightInfo>();
    }

    public static int MetadataSize(FArchive Ar)
    {
        var bNewWeightFormat = FAnimObjectVersion.Get(Ar) >= FAnimObjectVersion.Type.UnlimitedBoneInfluences;
        var numBytes = 0;

        if (!Ar.Versions["SkeletalMesh.UseNewCookedFormat"])
        {
            numBytes = 2 * 4; // bool + uint
        }
        else if (!bNewWeightFormat)
        {
            numBytes = 3 * 4; // bool + uint + uint
        }
        else
        {
            numBytes = 4 * 4; // bool + uint + uint + uint
            if (FAnimObjectVersion.Get(Ar) >= FAnimObjectVersion.Type.IncreaseBoneIndexLimitPerChunk)
                numBytes += 4; // bool（bUse16BitBoneIndex）
        }

        if (bNewWeightFormat)
        {
            numBytes += 4; // FStripDataFlags
        }

        return numBytes;
    }
}