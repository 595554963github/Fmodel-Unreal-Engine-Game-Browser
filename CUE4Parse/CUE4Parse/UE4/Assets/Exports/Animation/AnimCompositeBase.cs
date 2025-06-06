using System;
using System.Diagnostics.CodeAnalysis;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.Animation
{
    public abstract class UAnimCompositeBase : UAnimSequenceBase { }

    [StructFallback]
    public class FAnimSegment
    {
        public FPackageIndex AnimReference { get; }
        public float StartPos { get; }
        public float AnimStartTime { get; }
        public float AnimEndTime { get; }
        public float AnimPlayRate { get; }
        public int LoopingCount { get; }

        public FAnimSegment(FStructFallback fallback)
        {
            AnimReference = fallback.GetOrDefault<FPackageIndex>(nameof(AnimReference)) ?? new FPackageIndex();
            StartPos = fallback.GetOrDefault(nameof(StartPos), 0.0f);
            AnimStartTime = fallback.GetOrDefault(nameof(AnimStartTime), 0.0f);
            AnimEndTime = fallback.GetOrDefault(nameof(AnimEndTime), 0.0f);
            AnimPlayRate = fallback.GetOrDefault(nameof(AnimPlayRate), 1.0f);
            LoopingCount = fallback.GetOrDefault(nameof(LoopingCount), 0);
        }

        public float GetValidPlayRate()
        {
            UAnimSequenceBase? sequenceBase = null;

            if (AnimReference.IsNull || !AnimReference.TryLoad<UAnimSequenceBase>(out sequenceBase))
                return 1.0f;

            float seqPlayRate = sequenceBase!.RateScale;
            float finalPlayRate = seqPlayRate * AnimPlayRate;
            return UnrealMath.IsNearlyZero(finalPlayRate) ? 1.0f : finalPlayRate;
        }

        public float GetLength()
        {
            if (LoopingCount <= 0 || Math.Abs(AnimEndTime - AnimStartTime) < float.Epsilon)
                return 0.0f;

            return (LoopingCount * (AnimEndTime - AnimStartTime)) / Math.Abs(GetValidPlayRate());
        }
    }

    [StructFallback]
    public class FAnimTrack
    {
        public FAnimSegment[] AnimSegments { get; }

        public FAnimTrack(FStructFallback fallback)
        {
            AnimSegments = fallback.GetOrDefault<FAnimSegment[]>(nameof(AnimSegments)) ?? Array.Empty<FAnimSegment>();
        }

        public float GetLength()
        {
            if (AnimSegments == null || AnimSegments.Length == 0)
                return 0.0f;

            float totalLength = 0.0f;

            foreach (var animSegment in AnimSegments)
            {
                if (animSegment == null)
                    continue;

                var endFrame = animSegment.StartPos + animSegment.GetLength();
                if (endFrame > totalLength)
                {
                    totalLength = endFrame;
                }
            }

            return totalLength;
        }
    }
}