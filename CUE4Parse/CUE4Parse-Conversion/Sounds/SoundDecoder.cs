using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Readers;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Sounds.ADPCM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CUE4Parse_Conversion.Sounds;

public static class SoundDecoder
{
    public static void Decode(this UObject export, bool shouldDecompress, out string audioFormat, out byte[]? data)
    {
        switch (export)
        {
            case UAkMediaAssetData mediaAsset: mediaAsset.Decode(shouldDecompress, out audioFormat, out data); break;
            case USoundWave soundWave: soundWave.Decode(shouldDecompress, out audioFormat, out data); break;
            default: audioFormat = string.Empty; data = null; break;
        }
    }

    public static void Decode(this USoundWave soundWave, bool shouldDecompress, out string audioFormat, out byte[]? data)
    {
        audioFormat = string.Empty;
        byte[]? input = null;

        if (!soundWave.bStreaming)
        {
            if (soundWave.CompressedFormatData != null)
            {
                var compressedData = soundWave.CompressedFormatData.Formats.First();
                audioFormat = compressedData.Key.Text.SubstringBefore('_');
                input = compressedData.Value.Data;
            }

            if (soundWave.RawData?.Data != null) // is this even a thing?
            {
                audioFormat = string.Empty;
                input = soundWave.RawData.Data;
            }
        }
        else if (soundWave.RunningPlatformData?.Chunks != null)
        {
            var offset = 0;
            var ret = new byte[soundWave.RunningPlatformData.Chunks.Sum(x => x.AudioDataSize)];
            for (var i = 0; i < soundWave.RunningPlatformData.NumChunks; i++)
            {
                var chunkData = soundWave.RunningPlatformData.Chunks[i].BulkData.Data;
                if (chunkData != null)
                {
                    Buffer.BlockCopy(chunkData, 0, ret, offset, soundWave.RunningPlatformData.Chunks[i].AudioDataSize);
                    offset += soundWave.RunningPlatformData.Chunks[i].AudioDataSize;
                }
            }

            audioFormat = soundWave.RunningPlatformData.AudioFormat.Text;
            input = ret;
        }

        data = Decompress(shouldDecompress, ref audioFormat, input);
    }

    public static void Decode(this UAkMediaAssetData mediaData, bool shouldDecompress, out string audioFormat, out byte[]? data)
    {
        audioFormat = "WEM";
        data = null;
        if (mediaData?.DataChunks == null)
            return;
        var totalSize = 0;
        var validChunks = new List<(byte[] Data, int Length)>();
        foreach (var chunk in mediaData.DataChunks)
        {
            if (chunk == null || chunk.IsPrefetch)
                continue;
            var chunkData = chunk.Data?.Data;
            if (chunkData == null || chunkData.Length == 0)
                continue;
            validChunks.Add((chunkData, chunkData.Length));
            totalSize += chunkData.Length;
        }
        if (totalSize == 0 || validChunks.Count == 0)
            return;
        var input = new byte[totalSize];
        var offset = 0;

        foreach (var (chunkData, length) in validChunks)
        {
            if (offset + length > input.Length)
                break; 
            Buffer.BlockCopy(chunkData, 0, input, offset, length);
            offset += length;
        }
        data = Decompress(shouldDecompress, ref audioFormat, input);
    }
    public static byte[]? Decompress(bool shouldDecompress, ref string audioFormat, byte[]? input)
    {
        if (input == null) return null;
        if (!shouldDecompress) return input;
        if (audioFormat.Equals("ADPCM", StringComparison.OrdinalIgnoreCase) || audioFormat.Equals("PCM", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = new FByteArchive("WhoDoesntLoveCats", input);
            switch (ADPCMDecoder.GetAudioFormat(archive))
            {
                case EAudioFormat.WAVE_FORMAT_PCM:
                    audioFormat = "WAV";
                    return input;
                case EAudioFormat.WAVE_FORMAT_ADPCM:
                    return input;
            }
        }
        else if (audioFormat.Equals("BINKA", StringComparison.OrdinalIgnoreCase))
            return input;
        else if (audioFormat.Equals("RADA", StringComparison.OrdinalIgnoreCase))
            return input;
        else if (audioFormat.Equals("OPUS", StringComparison.OrdinalIgnoreCase))
            return input;
        else if (audioFormat.Equals("WEM", StringComparison.OrdinalIgnoreCase))
            return input;
        else if (audioFormat.Equals("AT9", StringComparison.OrdinalIgnoreCase))
            return input;
        else if (audioFormat.IndexOf("OGG", StringComparison.OrdinalIgnoreCase) > -1)
        {
            audioFormat = "OGG";
            return input;
        }

        return null;
    }
}