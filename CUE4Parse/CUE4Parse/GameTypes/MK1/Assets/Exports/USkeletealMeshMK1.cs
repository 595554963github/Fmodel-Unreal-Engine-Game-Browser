using System;
using CUE4Parse.GameTypes.MK1.Assets.Objects;
using CUE4Parse.UE4.Assets.Exports.Animation;

namespace CUE4Parse.UE4.Assets.Exports.SkeletalMesh;

public partial class USkeletalMesh : UObject
{
    public void PopulateMorphTargetVerticesDataMK1()
    {
        if (LODModels == null || LODModels.Length == 0)
            return;

        if (LODModels[0] is null || LODModels[0].Sections.Length == 0)
            LODModels = LODModels[1..];

        if (LODModels.Length == 0)
            return;

        var maxLodLevel = -1;
        for (int i = 0; i < LODModels.Length; i++)
        {
            if (LODModels[i].AdditionalBuffer is not null)
            {
                maxLodLevel = i + 1;
            }
        }

        if (maxLodLevel == -1)
            return;

        if (MorphTargets == null)
            return;

        for (int index = 0; index < MorphTargets.Length; index++)
        {
            UMorphTarget? morphTarget = null;
            if (MorphTargets[index] is null || !MorphTargets[index].TryLoad<UMorphTarget>(out morphTarget))
                continue;

            if (morphTarget == null)
                continue;

            var morphTargetLODModels = new FMorphTargetLODModel[maxLodLevel];
            for (var i = 0; i < maxLodLevel; i++)
            {
                morphTargetLODModels[i] = new FMorphTargetLODModel();

                if (i >= LODModels.Length || LODModels[i]?.AdditionalBuffer is not FMorphTargetVertexInfoBufferMK1 buffer)
                    continue;

                if (index >= buffer.Sizes.Length || index >= buffer.Offsets.Length)
                    continue;

                var size = buffer.Sizes[index];
                morphTargetLODModels[i].NumBaseMeshVerts = size;

                if (buffer.Vertices.Length >= buffer.Offsets[index] + size)
                {
                    morphTargetLODModels[i].Vertices = new FMorphTargetDelta[size];
                    Array.Copy(buffer.Vertices, buffer.Offsets[index], morphTargetLODModels[i].Vertices, 0, size);
                }
            }

            if (morphTargetLODModels.Length > 0)
            {
                morphTarget.MorphLODModels = morphTargetLODModels;
            }
        }
    }
}