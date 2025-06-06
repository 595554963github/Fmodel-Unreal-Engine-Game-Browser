using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using System;

namespace CUE4Parse.UE4.Objects.Engine.Animation
{
    public class UPoseAsset : UAnimationAsset
    {
        public FPoseDataContainer PoseContainer { get; private set; } = new();
        public bool bAdditivePose { get; private set; }
        public int BasePoseIndex { get; private set; }
        public FName RetargetSource { get; private set; } = new();
        public FTransform[] RetargetSourceAssetReferencePose { get; private set; } = Array.Empty<FTransform>();

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            PoseContainer = GetOrDefault(nameof(PoseContainer), new FPoseDataContainer());
            bAdditivePose = GetOrDefault(nameof(bAdditivePose), false);
            BasePoseIndex = GetOrDefault(nameof(BasePoseIndex), 0);
            RetargetSource = GetOrDefault(nameof(RetargetSource), new FName());
            RetargetSourceAssetReferencePose = GetOrDefault(nameof(RetargetSourceAssetReferencePose), Array.Empty<FTransform>());
        }
    }

    [StructFallback]
    public class FPoseData
    {
        public FTransform[] LocalSpacePose { get; } = Array.Empty<FTransform>();
        public float[] CurveData { get; } = Array.Empty<float>();
        public Dictionary<int, int> TrackToBufferIndex { get; } = new();

        public FPoseData(FStructFallback fallback)
        {
            LocalSpacePose = fallback.GetOrDefault(nameof(LocalSpacePose), Array.Empty<FTransform>());
            CurveData = fallback.GetOrDefault(nameof(CurveData), Array.Empty<float>());

            if (fallback.GetOrDefault(nameof(TrackToBufferIndex), (UScriptMap?)null) is { } trackToBufferIndexMap)
            {
                foreach (var (key, value) in trackToBufferIndexMap.Properties)
                {
                    if (value is null) continue;

                    var trackIndex = key.GetValue<int>();
                    var bufferIndex = value.GetValue<int>();

                    TrackToBufferIndex[trackIndex] = bufferIndex;
                }
            }
        }
    }

    [StructFallback]
    public class FPoseAssetInfluence
    {
        public int BoneTransformIndex { get; }
        public int PoseIndex { get; }

        public FPoseAssetInfluence(FStructFallback fallback)
        {
            BoneTransformIndex = fallback.GetOrDefault(nameof(BoneTransformIndex), 0);
            PoseIndex = fallback.GetOrDefault(nameof(PoseIndex), 0);
        }
    }

    [StructFallback]
    public class FPoseAssetInfluences
    {
        public FPoseAssetInfluence[] Influences { get; } = Array.Empty<FPoseAssetInfluence>();

        public FPoseAssetInfluences(FStructFallback fallback)
        {
            Influences = fallback.GetOrDefault(nameof(Influences), Array.Empty<FPoseAssetInfluence>());
        }
    }

    [StructFallback]
    public class FPoseDataContainer
    {
        public FSmartName[] PoseNames_DEPRECATED { get; } = Array.Empty<FSmartName>();
        public FName[] PoseFNames { get; } = Array.Empty<FName>();
        public FName[] Tracks { get; } = Array.Empty<FName>();
        public FPoseAssetInfluences[] TrackPoseInfluenceIndices { get; } = Array.Empty<FPoseAssetInfluences>();
        public FPoseData[] Poses { get; } = Array.Empty<FPoseData>();
        public FAnimCurveBase[] Curves { get; } = Array.Empty<FAnimCurveBase>();

        public FPoseDataContainer() { }

        public FPoseDataContainer(FStructFallback fallback)
        {
            PoseNames_DEPRECATED = fallback.GetOrDefault(nameof(PoseNames_DEPRECATED), Array.Empty<FSmartName>());
            PoseFNames = fallback.GetOrDefault(nameof(PoseFNames), Array.Empty<FName>());
            Tracks = fallback.GetOrDefault(nameof(Tracks), Array.Empty<FName>());
            TrackPoseInfluenceIndices = fallback.GetOrDefault(nameof(TrackPoseInfluenceIndices), Array.Empty<FPoseAssetInfluences>());
            Poses = fallback.GetOrDefault(nameof(Poses), Array.Empty<FPoseData>());
            Curves = fallback.GetOrDefault(nameof(Curves), Array.Empty<FAnimCurveBase>());
        }

        public IEnumerable<string> GetPoseNames()
        {
            if (PoseNames_DEPRECATED.Length > 0)
            {
                return PoseNames_DEPRECATED.Select(x => x.DisplayName.Text);
            }

            if (PoseFNames.Length > 0)
            {
                return PoseFNames.Select(x => x.Text);
            }

            return Enumerable.Empty<string>();
        }
    }
}