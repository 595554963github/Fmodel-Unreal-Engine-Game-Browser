using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Engine.Animation;
using CUE4Parse.UE4.Objects.Engine.Curves;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.Animation
{
    public enum EAnimAssetCurveFlags
    {
        AACF_NONE = 0,
        AACF_Editable = 0x00000004,
        AACF_Metadata = 0x00000010,
    }

    [StructFallback]
    public class FAnimCurveBase
    {
        public FName CurveName;
        public int CurveTypeFlags;

        public FAnimCurveBase()
        {
            CurveName = new FName();
            CurveTypeFlags = (int)EAnimAssetCurveFlags.AACF_NONE;
        }

        public FAnimCurveBase(FStructFallback data)
        {
            var curveName = data.GetOrDefault<FName>(nameof(CurveName));
            CurveName = curveName.IsNone ? new FName() : curveName;
            CurveTypeFlags = data.GetOrDefault(nameof(CurveTypeFlags), (int)EAnimAssetCurveFlags.AACF_NONE);
        }
    }

    public class FFloatCurve : FAnimCurveBase
    {
        public FRichCurve FloatCurve { get; private set; }

        public FFloatCurve() : base()
        {
            FloatCurve = new FRichCurve();
        }

        public FFloatCurve(FStructFallback data) : base(data)
        {
            FloatCurve = data.GetOrDefault<FRichCurve>(nameof(FloatCurve)) ?? new FRichCurve();
        }
    }

    public struct FRawCurveTracks
    {
        public FFloatCurve[]? FloatCurves;

        public FRawCurveTracks(FStructFallback data)
        {
            FloatCurves = data.GetOrDefault<FFloatCurve[]>(nameof(FloatCurves));
        }
    }
}