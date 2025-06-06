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
    public const uint PAK_FILE_MAGIC_�������� = 0xA590ED1E;
    public const uint PAK_FILE_MAGIC_���֮��_���� = 0x6B2A56B8;
    public const uint PAK_FILE_MAGIC_����ͻϮ = 0xA4CCD123;
    public const uint PAK_FILE_MAGIC_�������� = 0x5A6F12EC;
    public const uint PAK_FILE_MAGIC_ʮ���������� = 0x65617441;
    public const uint PAK_FILE_MAGIC_Ԫ��֮�� = 0x1B6A32F1;
    public const uint PAK_FILE_MAGIC_��ƽ��Ӣ = 0xff67ff70;
    public const uint PAK_FILE_MAGIC_���ܿ�����_Ư�� = 0x81c4b35b;
    public const uint PAK_FILE_MAGIC_�۷弫�� = 0x9a51da3f;
    public const uint PAK_FILE_MAGIC_���� = 0x22ce976a;
    public const uint PAK_FILE_MAGIC_��Ը������ = 0x11adde11;

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
        if (Ar.Game == EGame.GAME_���� && offsetToTry == OffsetsToTry.SizeHotta)
        {
            hottaVersion = Ar.Read<uint>();
            if (hottaVersion > 255)
            {
                hottaVersion = 0;
            }
        }

        if (Ar.Game == EGame.GAME_���֮��_����) Ar.Position += 3;

        if (Ar.Game == EGame.GAME_��ƽ��Ӣ)
        {
            EncryptionKeyGuid = default;
            EncryptedIndex = Ar.Read<byte>() != 0x6c;
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_��ƽ��Ӣ) return;
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

        if (Ar.Game == EGame.GAME_�۷弫��)
        {
            EncryptedIndex = Ar.ReadFlag();
            EncryptionKeyGuid = Ar.Read<FGuid>();
            CustomEncryptionData = Ar.ReadBytes(4);
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_�۷弫��) return;
            IndexSize = Ar.Read<long>();
            IndexHash = new FSHAHash(Ar);
            Version = Ar.Read<EPakFileVersion>();
            IndexOffset = Ar.Read<long>();
            goto beforeCompression;
        }

        if (Ar.Game == EGame.GAME_��Ը������)
        {
            EncryptionKeyGuid = Ar.Read<FGuid>();
            EncryptedIndex = Ar.ReadFlag();
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_��Ը������)
                return;
            IndexHash = new FSHAHash(Ar);
            Version = (EPakFileVersion)(11 + (Ar.Read<int>() ^ 0x0A4FFC11));
            Ar.Position += 8;
            IndexSize = Ar.Read<long>() ^ 0x0BBEFB6F91D3B57B;
            IndexOffset = Ar.Read<long>();
            goto beforeCompression;
        }

        if (Ar.Game == EGame.GAME_����)
        {
            EncryptedIndex = Ar.ReadFlag();
            Version = Ar.Read<EPakFileVersion>();
            IndexSize = Ar.Read<long>();
            IndexHash = new FSHAHash(Ar);
            IndexOffset = Ar.Read<long>();
            Magic = Ar.Read<uint>();
            if (Magic != PAK_FILE_MAGIC_����)
                return;
            EncryptionKeyGuid = Ar.Read<FGuid>();
            goto beforeCompression;
        }

        if (Ar.Game == EGame.GAME_ɳ��_����)
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
            if (Ar.Game == EGame.GAME_�������� && Magic == PAK_FILE_MAGIC_�������� ||
                Ar.Game == EGame.GAME_���֮��_���� && Magic == PAK_FILE_MAGIC_���֮��_���� ||
                Ar.Game == EGame.GAME_����ͻϮ && Magic == PAK_FILE_MAGIC_����ͻϮ ||
                Ar.Game == EGame.GAME_�������� && Magic == PAK_FILE_MAGIC_�������� ||
                Ar.Game == EGame.GAME_ʮ���������� && Magic == PAK_FILE_MAGIC_ʮ���������� ||
                Ar.Game == EGame.GAME_Ԫ��֮�� && Magic == PAK_FILE_MAGIC_Ԫ��֮�� ||
                Ar.Game == EGame.GAME_���ܿ�����_Ư�� && Magic == PAK_FILE_MAGIC_���ܿ�����_Ư��)
                goto afterMagic;
            return;
        }

    afterMagic:
        Version = hottaVersion >= 2 ? (EPakFileVersion)(Ar.Read<int>() ^ 2) : Ar.Read<EPakFileVersion>();
        if (Ar.Game == EGame.GAME_���ù���2)
        {
            Version &= (EPakFileVersion)0xFFFF;
        }

        if (Ar.Game == EGame.GAME_���ܿ�����_Ư��)
        {
            Version &= (EPakFileVersion)0x0F;
        }

        if (Ar.Game == EGame.GAME_ʮ����������)
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
        if (Ar.Game == EGame.GAME_���֮��_����) Ar.Position += 1;
        if (Ar.Game == EGame.GAME_����_���) Ar.Position += 2;
        IndexOffset = Ar.Read<long>();
        if (Ar.Game == EGame.GAME_��������) Ar.Position += 8;
        if (Ar.Game == EGame.GAME_���׽���) IndexOffset ^= 0x1C1D1E1F;
        if (Ar.Game == EGame.GAME_���ܿ�����_Ư��) IndexOffset ^= 0x3009EB;
        IndexSize = Ar.Read<long>();
        IndexHash = new FSHAHash(Ar);

        if (Ar.Game == EGame.GAME_Ԫ��֮��)
        {
            (IndexOffset, IndexSize) = (IndexSize, IndexOffset);
        }

        if (Ar.Game == EGame.GAME_���������� && offsetToTry == OffsetsToTry.SizeHotta && Version >= EPakFileVersion.PakFile_Version_Latest)
        {
            var mymVersion = Ar.Read<uint>();
        }

        if (Ar.Game == EGame.GAME_����ͻϮ)
        {
            EncryptionKeyGuid = default;
            IndexOffset = (long)((ulong)IndexOffset ^ 0xD5B9B05CE8143A3C) - 0xAA;
            IndexSize = (long)((ulong)IndexSize ^ 0x6DB425B4BC084B4B) - 0xA8;
        }

        if (Ar.Game == EGame.GAME_����ɱ��)
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
                var length = Ar.Game == EGame.GAME_���ܿ�����_Ư�� ? 48 : COMPRESSION_METHOD_NAME_LEN;
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
                        Log.Warning($"��{Ar.Name}�з���δ֪ѹ������'{name}'");
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
                EGame.GAME_ɳ��_���� => (long)OffsetsToTry.SizeDuneAwakening,
                EGame.GAME_���ܿ�����_Ư�� => (long)OffsetsToTry.SizeKartRiderDrift,
                _ => (long)OffsetsToTry.SizeMax,
            };

            if (length < maxOffset)
            {
                throw new ParserException($"�ļ�{Ar.Name}̫С����������pak�ļ�");
            }
            Ar.Seek(-maxOffset, SeekOrigin.End);
            var buffer = stackalloc byte[(int)maxOffset];
            Ar.Serialize(buffer, (int)maxOffset);

            if (Ar.Game == EGame.GAME_������)
            {
                DecryptInZOIFPakInfo(Ar, maxOffset, buffer);
            }

            var reader = new FPointerArchive(Ar.Name, buffer, maxOffset, Ar.Versions);

            var offsetsToTry = Ar.Game switch
            {
                EGame.GAME_���� or EGame.GAME_���������� or EGame.GAME_���֮��_���� => new[] { OffsetsToTry.SizeHotta },
                EGame.GAME_ʮ���������� => new[] { OffsetsToTry.SizeFTT },
                EGame.GAME_����ɱ�� => new[] { OffsetsToTry.SizeDbD },
                EGame.GAME_�������� => new[] { OffsetsToTry.SizeFarlight },
                EGame.GAME_QQ_��tm����Ϸ or EGame.GAME_Ԫ��֮�� => new[] { OffsetsToTry.SizeDreamStar, OffsetsToTry.SizeQQ },
                EGame.GAME_��ƽ��Ӣ => new[] { OffsetsToTry.SizeGameForPeace },
                EGame.GAME_����_��� => new[] { OffsetsToTry.SizeB1 },
                EGame.GAME_Rennsport => new[] { OffsetsToTry.SizeRennsport },
                EGame.GAME_�۷弫�� => new[] { OffsetsToTry.SiseRacingMaster },
                EGame.GAME_����_�������� or EGame.GAME_��Ը������ => new[] { OffsetsToTry.Size_ARKSurvivalAscended },
                EGame.GAME_���ܿ�����_Ư�� => [.. _offsetsToTry, OffsetsToTry.SizeKartRiderDrift],
                EGame.GAME_ɳ��_���� => new[] { OffsetsToTry.SizeDuneAwakening },
                _ => _offsetsToTry
            };

            foreach (var offset in offsetsToTry)
            {
                reader.Seek(-(long)offset, SeekOrigin.End);
                var info = new FPakInfo(reader, offset);

                var found = Ar.Game switch
                {
                    EGame.GAME_ʮ���������� when info.Magic == PAK_FILE_MAGIC_ʮ���������� => true,
                    EGame.GAME_��ƽ��Ӣ when info.Magic == PAK_FILE_MAGIC_��ƽ��Ӣ => true,
                    EGame.GAME_�������� when info.Magic == PAK_FILE_MAGIC_�������� => true,
                    EGame.GAME_���֮��_���� when info.Magic == PAK_FILE_MAGIC_���֮��_���� => true,
                    EGame.GAME_Ԫ��֮�� when info.Magic == PAK_FILE_MAGIC_Ԫ��֮�� => true,
                    EGame.GAME_�۷弫�� when info.Magic == PAK_FILE_MAGIC_�۷弫�� => true,
                    EGame.GAME_��Ը������ when info.Magic == PAK_FILE_MAGIC_��Ը������ => true,
                    EGame.GAME_���� when info.Magic == PAK_FILE_MAGIC_���� => true,
                    EGame.GAME_�������� when info.Magic == PAK_FILE_MAGIC_�������� => true,
                    EGame.GAME_����ͻϮ when info.Magic == PAK_FILE_MAGIC_����ͻϮ => true,
                    EGame.GAME_���ܿ�����_Ư�� when info.Magic == PAK_FILE_MAGIC_���ܿ�����_Ư�� => true,
                    _ => info.Magic == PAK_FILE_MAGIC
                };
                if (found) return info;
            }
        }
        throw new ParserException($"�ļ�{Ar.Name}��ʽδ֪");
    }
}