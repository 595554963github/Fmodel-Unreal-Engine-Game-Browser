using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine.Curves;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace CUE4Parse.UE4.Objects.MovieScene
{
    public readonly struct FMovieSceneChannel<T> : IUStruct
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public readonly ERichCurveExtrapolation PreInfinityExtrap;
        [JsonConverter(typeof(StringEnumConverter))]
        public readonly ERichCurveExtrapolation PostInfinityExtrap;
        public readonly FFrameNumber[] Times;
        public readonly FMovieSceneValue<T>[] Values;
        public readonly T? DefaultValue;
        public readonly bool bHasDefaultValue;
        public readonly FFrameRate TickResolution;
        public readonly bool bShowCurve;
        public readonly FStructFallback? StructFallback;

        public FMovieSceneChannel(FAssetArchive Ar)
        {
            PreInfinityExtrap = ERichCurveExtrapolation.RCCE_None;
            PostInfinityExtrap = ERichCurveExtrapolation.RCCE_None;
            Times = Array.Empty<FFrameNumber>();
            Values = Array.Empty<FMovieSceneValue<T>>();
            DefaultValue = default;
            bHasDefaultValue = false;
            TickResolution = default;
            bShowCurve = false;
            StructFallback = null;

            if (FSequencerObjectVersion.Get(Ar) < FSequencerObjectVersion.Type.SerializeFloatChannelCompletely &&
                FFortniteMainBranchObjectVersion.Get(Ar) < FFortniteMainBranchObjectVersion.Type.SerializeFloatChannelShowCurve)
            {
                StructFallback = new FStructFallback(Ar, "MovieSceneChannel");
                return;
            }

            if (Ar.Game == EGame.GAME_三角洲行动)
            {
                StructFallback = new FStructFallback(Ar, "MovieSceneChannel");
                return;
            }

            PreInfinityExtrap = Ar.Read<ERichCurveExtrapolation>();
            PostInfinityExtrap = Ar.Read<ERichCurveExtrapolation>();

            var serializedElementSize = Ar.Read<int>();
            Times = Ar.ReadArray<FFrameNumber>() ?? Array.Empty<FFrameNumber>();

            serializedElementSize = Ar.Read<int>();
            Values = Ar.ReadArray(() => new FMovieSceneValue<T>(Ar, Ar.Read<T>())) ?? Array.Empty<FMovieSceneValue<T>>();

            if (Ar.Game == EGame.GAME_双影奇境)
                Ar.SkipBulkArrayData(); // Duplicated Values array

            DefaultValue = Ar.Read<T>();
            bHasDefaultValue = Ar.ReadBoolean();
            TickResolution = Ar.Read<FFrameRate>();

            bShowCurve = FFortniteMainBranchObjectVersion.Get(Ar) >= FFortniteMainBranchObjectVersion.Type.SerializeFloatChannelShowCurve &&
                         Ar.ReadBoolean();

            if (Ar.Game == EGame.GAME_双影奇境)
                Ar.Position += 4;
        }
    }
}