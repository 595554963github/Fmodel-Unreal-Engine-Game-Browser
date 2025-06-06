using System;
using System.Collections.Generic;
using System.IO;
using CUE4Parse.Compression;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Serilog;

namespace CUE4Parse.UE4.Pak.Objects;

public enum EPakFileVersion
{
    PakFile_Version_Initial = 1,
    PakFile_Version_NoTimestamps = 2,
    PakFile_Version_CompressionEncryption = 3,
    PakFile_Version_IndexEncryption = 4,
    PakFile_Version_RelativeChunkOffsets = 5,
    PakFile_Version_DeleteRecords = 6,
    PakFile_Version_EncryptionKeyGuid = 7,
    PakFile_Version_FNameBasedCompressionMethod = 8,
    PakFile_Version_FrozenIndex = 9,
    PakFile_Version_PathHashIndex = 10,
    PakFile_Version_Fnv64BugFix = 11,

    PakFile_Version_Last,
    PakFile_Version_Invalid,
    PakFile_Version_Latest = PakFile_Version_Last - 1
}

public partial class FPakInfo
{
    public const uint PAK_FILE_MAGIC = 0x5A6F12E1;
    public const uint PAK_FILE_MAGIC_逃生试炼 = 0xA590ED1E;
    public const uint PAK_FILE_MAGIC_火炬之光_无限 = 0x6B2A56B8;
    public const uint PAK_FILE_MAGIC_兽猎突袭 = 0xA4CCD123;
    public const uint PAK_FILE_MAGIC_黎明觉醒 = 0x5A6F12EC;
    public const uint PAK_FILE_MAGIC_十三号星期五 = 0x65617441;
    public const uint PAK_FILE_MAGIC_元梦之星 = 0x1B6A32F1;
    public const uint PAK_FILE_MAGIC_和平精英 = 0xff67ff70;
    public const uint PAK_FILE_MAGIC_跑跑卡丁车_漂移 = 0x81c4b35b;
    public const uint PAK_FILE_MAGIC_巅峰极速 = 0x9a51da3f;
    public const uint PAK_FILE_MAGIC_晶核 = 0x22ce976a;
    public const uint PAK_FILE_MAGIC_达愿福神社 = 0x11adde11;

    public const int COMPRESSION_METHOD_NAME_LEN = 32;

    public readonly uint Magic;
    public readonly EPakFileVersion Version;
    public readonly bool IsSubVersion;
    public readonly long IndexOffset;
    public readonly long IndexSize;
    public readonly FSHAHash IndexHash;
    public readonly bool EncryptedIndex;
    public readonly bool IndexIsFrozen;
    public readonly FGuid EncryptionKeyGuid;
    public readonly List<CompressionMethod> CompressionMethods;
    public readonly byte[] CustomEncryptionData;

    private FPakInfo(FArchive Ar, OffsetsToTry offsetToTry)
    {
        CompressionMethods = new List<CompressionMethod>();
        CustomEncryptionData = Array.Empty<byte>();

        var hottaVersion = 0u;
        if (Ar.Game == EGame.GAME_幻塔 && offsetToTry == OffsetsToTry.SizeHotta)
        {
            hottaVersion = Ar.Read<uint>();
            if (hottaVersion > 255)
            {
                hottaVersion = 0;
            }
        }

        if (Ar.Game == EGame.GAME_火炬之光_无限) Ar.Position += 3;

        if (Ar.Game == EGame.GAME_和平精英)
        {
            EncryptionKeyGuid = default;
            EncryptedIndex = Ar.Read<byte>() != 0x6c;
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_和平精英) return;
            Version = Ar.Read<EPakFileVersion>();
            if (Version >= EPakFileVersion.PakFile_Version_PathHashIndex)
            {
                Version = EPakFileVersion.PakFile_Version_FNameBasedCompressionMethod;
            }
            IndexHash = new FSHAHash(Ar);
            IndexSize = (long)(Ar.Read<ulong>() ^ 0x8924b0e3298b7069);
            IndexOffset = (long)(Ar.Read<ulong>() ^ 0xd74af37faa6b020d);
            CompressionMethods = new List<CompressionMethod>
            {
                CompressionMethod.None, CompressionMethod.Zlib, CompressionMethod.Gzip, CompressionMethod.Oodle,
                CompressionMethod.LZ4, CompressionMethod.Zstd
            };
            return;
        }

        if (Ar.Game == EGame.GAME_巅峰极速)
        {
            EncryptedIndex = Ar.ReadFlag();
            EncryptionKeyGuid = Ar.Read<FGuid>();
            CustomEncryptionData = Ar.ReadBytes(4);
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_巅峰极速) return;
            IndexSize = Ar.Read<long>();
            IndexHash = new FSHAHash(Ar);
            Version = Ar.Read<EPakFileVersion>();
            IndexOffset = Ar.Read<long>();
            goto beforeCompression;
        }

        if (Ar.Game == EGame.GAME_达愿福神社)
        {
            EncryptionKeyGuid = Ar.Read<FGuid>();
            EncryptedIndex = Ar.ReadFlag();
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_达愿福神社)
                return;
            IndexHash = new FSHAHash(Ar);
            Version = (EPakFileVersion)(11 + (Ar.Read<int>() ^ 0x0A4FFC11));
            Ar.Position += 8;
            IndexSize = Ar.Read<long>() ^ 0x0BBEFB6F91D3B57B;
            IndexOffset = Ar.Read<long>();
            goto beforeCompression;
        }

        if (Ar.Game == EGame.GAME_晶核)
        {
            EncryptedIndex = Ar.ReadFlag();
            Version = Ar.Read<EPakFileVersion>();
            IndexSize = Ar.Read<long>();
            IndexHash = new FSHAHash(Ar);
            IndexOffset = Ar.Read<long>();
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_晶核)
                return;
            EncryptionKeyGuid = Ar.Read<FGuid>();
            goto beforeCompression;
        }

        if (Ar.Game == EGame.GAME_沙丘_觉醒)
        {
            var magic = Ar.Read<uint>();
            if (magic != 0xA590ED1E) return;
            IndexOffset = Ar.Read<long>();
            IndexSize = Ar.Read<long>();
            IndexHash = new FSHAHash(Ar);
            EncryptionKeyGuid = Ar.Read<FGuid>();
            EncryptedIndex = Ar.ReadFlag();
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC) return;
            Version = Ar.Read<EPakFileVersion>();
            Ar.Position += 36;
            goto beforeCompression;
        }

        EncryptionKeyGuid = Ar.Read<FGuid>();
        EncryptedIndex = Ar.Read<byte>() != 0;

        Magic = Ar.Read<uint>();
        if (Magic != PAK_FILE_MAGIC)
        {
            if (Ar.Game == EGame.GAME_逃生试炼 && Magic == PAK_FILE_MAGIC_逃生试炼 ||
                Ar.Game == EGame.GAME_火炬之光_无限 && Magic == PAK_FILE_MAGIC_火炬之光_无限 ||
                Ar.Game == EGame.GAME_兽猎突袭 && Magic == PAK_FILE_MAGIC_兽猎突袭 ||
                Ar.Game == EGame.GAME_黎明觉醒 && Magic == PAK_FILE_MAGIC_黎明觉醒 ||
                Ar.Game == EGame.GAME_十三号星期五 && Magic == PAK_FILE_MAGIC_十三号星期五 ||
                Ar.Game == EGame.GAME_元梦之星 && Magic == PAK_FILE_MAGIC_元梦之星 ||
                Ar.Game == EGame.GAME_跑跑卡丁车_漂移 && Magic == PAK_FILE_MAGIC_跑跑卡丁车_漂移)
                goto afterMagic;
            return;
        }

    afterMagic:
        Version = hottaVersion >= 2 ? (EPakFileVersion)(Ar.Read<int>() ^ 2) : Ar.Read<EPakFileVersion>();
        if (Ar.Game == EGame.GAME_腐烂国度2)
        {
            Version &= (EPakFileVersion)0xFFFF;
        }

        if (Ar.Game == EGame.GAME_跑跑卡丁车_漂移)
        {
            Version &= (EPakFileVersion)0x0F;
        }

        if (Ar.Game == EGame.GAME_十三号星期五)
        {
            if (!EncryptedIndex && Magic == 0 && (int)Version == PAK_FILE_MAGIC)
            {
                Magic = PAK_FILE_MAGIC;
                Version = Ar.Read<EPakFileVersion>();
            }

            if (Version >= EPakFileVersion.PakFile_Version_RelativeChunkOffsets)
            {
                Version = EPakFileVersion.PakFile_Version_IndexEncryption;
                Ar.Position += 4;
            }
        }

        IsSubVersion = Version == EPakFileVersion.PakFile_Version_FNameBasedCompressionMethod && offsetToTry == OffsetsToTry.Size8a;
        if (Ar.Game == EGame.GAME_火炬之光_无限) Ar.Position += 1;
        if (Ar.Game == EGame.GAME_黑神话_悟空) Ar.Position += 2;
        IndexOffset = Ar.Read<long>();
        if (Ar.Game == EGame.GAME_落日余晖) Ar.Position += 8;
        if (Ar.Game == EGame.GAME_尘白禁区) IndexOffset ^= 0x1C1D1E1F;
        if (Ar.Game == EGame.GAME_跑跑卡丁车_漂移) IndexOffset ^= 0x3009EB;
        IndexSize = Ar.Read<long>();
        IndexHash = new FSHAHash(Ar);

        if (Ar.Game == EGame.GAME_元梦之星)
        {
            (IndexOffset, IndexSize) = (IndexSize, IndexOffset);
        }

        if (Ar.Game == EGame.GAME_遇见造物主 && offsetToTry == OffsetsToTry.SizeHotta && Version >= EPakFileVersion.PakFile_Version_Latest)
        {
            var mymVersion = Ar.Read<uint>();
        }

        if (Ar.Game == EGame.GAME_兽猎突袭)
        {
            EncryptionKeyGuid = default;
            IndexOffset = (long)((ulong)IndexOffset ^ 0xD5B9B05CE8143A3C) - 0xAA;
            IndexSize = (long)((ulong)IndexSize ^ 0x6DB425B4BC084B4B) - 0xA8;
        }

        if (Ar.Game == EGame.GAME_黎明杀机)
        {
            CustomEncryptionData = Ar.ReadBytes(28);
            _ = Ar.Read<uint>();
        }

        if (Version == EPakFileVersion.PakFile_Version_FrozenIndex)
        {
            IndexIsFrozen = Ar.Read<byte>() != 0;
        }

    beforeCompression:
        if (Version < EPakFileVersion.PakFile_Version_FNameBasedCompressionMethod)
        {
            CompressionMethods = new List<CompressionMethod>
            {
                CompressionMethod.None, CompressionMethod.Zlib, CompressionMethod.Gzip, CompressionMethod.Oodle, CompressionMethod.LZ4, CompressionMethod.Zstd
            };
        }
        else
        {
            var maxNumCompressionMethods = offsetToTry switch
            {
                OffsetsToTry.Size8a => 5,
                OffsetsToTry.SizeHotta => 5,
                OffsetsToTry.SizeDbD => 5,
                OffsetsToTry.SizeRennsport => 5,
                OffsetsToTry.Size8 => 4,
                OffsetsToTry.Size8_1 => 1,
                OffsetsToTry.Size8_2 => 2,
                OffsetsToTry.Size8_3 => 3,
                _ => 4
            };

            unsafe
            {
                var length = Ar.Game == EGame.GAME_跑跑卡丁车_漂移 ? 48 : COMPRESSION_METHOD_NAME_LEN;
                var bufferSize = length * maxNumCompressionMethods;
                var buffer = stackalloc byte[bufferSize];
                Ar.Serialize(buffer, bufferSize);
                CompressionMethods = new List<CompressionMethod>(maxNumCompressionMethods + 1)
                {
                    CompressionMethod.None
                };
                for (var i = 0; i < maxNumCompressionMethods; i++)
                {
                    var name = new string((sbyte*)buffer + i * length, 0, length).TrimEnd('\0');
                    if (string.IsNullOrEmpty(name))
                        continue;
                    if (!Enum.TryParse(name, true, out CompressionMethod method))
                    {
                        Log.Warning($"在{Ar.Name}中发现未知压缩方法'{name}'");
                        method = CompressionMethod.Unknown;
                    }
                    CompressionMethods.Add(method);
                }
                if (hottaVersion >= 3)
                {
                    CompressionMethods.Remove(0);
                }
            }
        }

        if (Version < EPakFileVersion.PakFile_Version_IndexEncryption)
        {
            EncryptedIndex = default;
        }

        if (Version < EPakFileVersion.PakFile_Version_EncryptionKeyGuid)
        {
            EncryptionKeyGuid = default;
        }
    }

    private enum OffsetsToTry
    {
        Size = sizeof(int) * 2 + sizeof(long) * 2 + 20 + 1 + 16,
        SizeGameForPeace = 45,
        Size8_1 = Size + 32,
        Size8_2 = Size8_1 + 32,
        Size8_3 = Size8_2 + 32,
        Size8 = Size8_3 + 32,
        Size8a = Size8 + 32,
        Size9 = Size8a + 1,
        SizeB1 = Size9 + 1,

        SiseRacingMaster = Size8 + 4,
        SizeFTT = Size + 4,
        SizeHotta = Size8a + 4,
        Size_ARKSurvivalAscended = Size8a + 8,
        SizeFarlight = Size8a + 9,
        SizeDreamStar = Size8a + 10,
        SizeRennsport = Size8a + 16,
        SizeQQ = Size8a + 26,
        SizeDbD = Size8a + 32,

        SizeLast,
        SizeMax = SizeLast - 1,
        SizeDuneAwakening = 261,
        SizeKartRiderDrift = 397,
    }

    private static readonly OffsetsToTry[] _offsetsToTry =
    {
        OffsetsToTry.Size8a,
        OffsetsToTry.Size8,
        OffsetsToTry.Size,
        OffsetsToTry.Size9,

        OffsetsToTry.Size8_1,
        OffsetsToTry.Size8_2,
        OffsetsToTry.Size8_3
    };

    public static FPakInfo ReadFPakInfo(FArchive Ar)
    {
        unsafe
        {
            var length = Ar.Length;
            var maxOffset = Ar.Game switch
            {
                EGame.GAME_沙丘_觉醒 => (long)OffsetsToTry.SizeDuneAwakening,
                EGame.GAME_跑跑卡丁车_漂移 => (long)OffsetsToTry.SizeKartRiderDrift,
                _ => (long)OffsetsToTry.SizeMax,
            };

            if (length < maxOffset)
            {
                throw new ParserException($"文件{Ar.Name}太小，不可能是pak文件");
            }
            Ar.Seek(-maxOffset, SeekOrigin.End);
            var buffer = stackalloc byte[(int)maxOffset];
            Ar.Serialize(buffer, (int)maxOffset);

            if (Ar.Game == EGame.GAME_云族裔)
            {
                DecryptInZOIFPakInfo(Ar, maxOffset, buffer);
            }

            var reader = new FPointerArchive(Ar.Name, buffer, maxOffset, Ar.Versions);

            var offsetsToTry = Ar.Game switch
            {
                EGame.GAME_幻塔 or EGame.GAME_遇见造物主 or EGame.GAME_火炬之光_无限 => new[] { OffsetsToTry.SizeHotta },
                EGame.GAME_十三号星期五 => new[] { OffsetsToTry.SizeFTT },
                EGame.GAME_黎明杀机 => new[] { OffsetsToTry.SizeDbD },
                EGame.GAME_落日余晖 => new[] { OffsetsToTry.SizeFarlight },
                EGame.GAME_QQ_这tm算游戏 or EGame.GAME_元梦之星 => new[] { OffsetsToTry.SizeDreamStar, OffsetsToTry.SizeQQ },
                EGame.GAME_和平精英 => new[] { OffsetsToTry.SizeGameForPeace },
                EGame.GAME_黑神话_悟空 => new[] { OffsetsToTry.SizeB1 },
                EGame.GAME_Rennsport => new[] { OffsetsToTry.SizeRennsport },
                EGame.GAME_巅峰极速 => new[] { OffsetsToTry.SiseRacingMaster },
                EGame.GAME_方舟_生存升级 or EGame.GAME_达愿福神社 => new[] { OffsetsToTry.Size_ARKSurvivalAscended },
                EGame.GAME_跑跑卡丁车_漂移 => [.. _offsetsToTry, OffsetsToTry.SizeKartRiderDrift],
                EGame.GAME_沙丘_觉醒 => new[] { OffsetsToTry.SizeDuneAwakening },
                _ => _offsetsToTry
            };

            foreach (var offset in offsetsToTry)
            {
                reader.Seek(-(long)offset, SeekOrigin.End);
                var info = new FPakInfo(reader, offset);

                var found = Ar.Game switch
                {
                    EGame.GAME_十三号星期五 when info.Magic == PAK_FILE_MAGIC_十三号星期五 => true,
                    EGame.GAME_和平精英 when info.Magic == PAK_FILE_MAGIC_和平精英 => true,
                    EGame.GAME_黎明觉醒 when info.Magic == PAK_FILE_MAGIC_黎明觉醒 => true,
                    EGame.GAME_火炬之光_无限 when info.Magic == PAK_FILE_MAGIC_火炬之光_无限 => true,
                    EGame.GAME_元梦之星 when info.Magic == PAK_FILE_MAGIC_元梦之星 => true,
                    EGame.GAME_巅峰极速 when info.Magic == PAK_FILE_MAGIC_巅峰极速 => true,
                    EGame.GAME_达愿福神社 when info.Magic == PAK_FILE_MAGIC_达愿福神社 => true,
                    EGame.GAME_晶核 when info.Magic == PAK_FILE_MAGIC_晶核 => true,
                    EGame.GAME_逃生试炼 when info.Magic == PAK_FILE_MAGIC_逃生试炼 => true,
                    EGame.GAME_兽猎突袭 when info.Magic == PAK_FILE_MAGIC_兽猎突袭 => true,
                    EGame.GAME_跑跑卡丁车_漂移 when info.Magic == PAK_FILE_MAGIC_跑跑卡丁车_漂移 => true,
                    _ => info.Magic == PAK_FILE_MAGIC
                };
                if (found) return info;
            }
        }
        throw new ParserException($"文件{Ar.Name}格式未知");
    }
}