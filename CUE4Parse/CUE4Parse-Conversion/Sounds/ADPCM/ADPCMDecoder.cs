using System;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse_Conversion.Sounds.ADPCM
{
    /// <summary>
    /// http://www-mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html
    /// </summary>
    public static class ADPCMDecoder
    {
        public static EAudioFormat GetAudioFormat(FArchive Ar)
        {
            var rfId = Ar.Read<EChunkIdentifier>();
            if (rfId != EChunkIdentifier.RIFF)
                throw new Exception($"无效的RIFF标识符,应为{EChunkIdentifier.RIFF}，实际为{rfId}）");

            var fileSize = Ar.Read<uint>();

            var wvId = Ar.Read<EChunkIdentifier>();
            if (wvId != EChunkIdentifier.WAVE)
                throw new Exception($"无效的WAVE标识符,应为{EChunkIdentifier.WAVE}，实际为{wvId}）");

            var ftId = Ar.Read<EChunkIdentifier>();
            if (ftId != EChunkIdentifier.FMT)
                throw new Exception($"无效的FMT标识符,应为{EChunkIdentifier.FMT}，实际为{ftId}）");

            var ftSize = Ar.Read<uint>();
            var savePos = Ar.Position;
            var wFormatTag = Ar.Read<EAudioFormat>();
            var nChannels = Ar.Read<ushort>();
            var nSamplesPerSec = Ar.Read<uint>();
            var nAvgBytesPerSec = Ar.Read<uint>();
            var nBlockAlign = Ar.Read<ushort>();
            var wBitsPerSample = Ar.Read<ushort>();
            if (wFormatTag <= EAudioFormat.WAVE_FORMAT_PCM)
            {
                Ar.Position = savePos + ftSize;
                return wFormatTag;
            }

            var cbSize = Ar.Read<ushort>();
            if (wFormatTag < EAudioFormat.WAVE_FORMAT_EXTENSIBLE)
            {
                Ar.Position = savePos + ftSize;
                return wFormatTag;
            }

            if (wBitsPerSample != (8 * nBlockAlign / nChannels))
                throw new Exception("原始位/样本字段与容器大小不匹配");

            var wValidBitsPerSample = Ar.Read<ushort>();
            var dwChannelMask = Ar.Read<uint>();
            wFormatTag = Ar.Read<EAudioFormat>();
            Ar.Position -= sizeof(EAudioFormat);
            var subFormat = Ar.Read<FGuid>();

            Ar.Position = savePos + ftSize;
            return wFormatTag;
        }
    }
}