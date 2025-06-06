using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.GameTypes.Rennsport.Encryption.Aes;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Pak.Objects;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using GenericReader;
using OffiUtils;
using static CUE4Parse.Compression.Compression;
using static CUE4Parse.UE4.Pak.Objects.EPakFileVersion;

namespace CUE4Parse.UE4.Pak
{
    public partial class PakFileReader : AbstractAesVfsReader
    {
        public readonly FArchive Ar;
        public readonly FPakInfo Info;

        public override string MountPoint { get; protected set; } = string.Empty;
        public sealed override long Length { get; set; }

        public override bool HasDirectoryIndex => true;
        public override FGuid EncryptionKeyGuid => Info.EncryptionKeyGuid;
        public override bool IsEncrypted => Info.EncryptedIndex;

        public PakFileReader(FArchive Ar) : base(Ar.Name, Ar.Versions)
        {
            this.Ar = Ar;
            Length = Ar.Length;
            Info = FPakInfo.ReadFPakInfo(Ar);

            if (Info.Version > PakFile_Version_Latest && !UsingCustomPakVersion())
            {
                Log.Warning($"Pak�ļ�\"{Name}\"�İ汾 {(int)Info.Version}����֧��");
            }
        }

        // ��Щ��Ϸʹ�ð汾 >= 12 ����ʾ���Զ����ʽ
        private bool UsingCustomPakVersion()
        {
            return Ar.Game switch
            {
                EGame.GAME_����ůů or EGame.GAME_���������� or EGame.GAME_����ɱ��
                    or EGame.GAME_���׽��� or EGame.GAME_���֮��_���� or EGame.GAME_����
                    or EGame.GAME_ȫ������_��� or EGame.GAME_QQ_��tm����Ϸ or EGame.GAME_Ԫ��֮�� => true,
                _ => false
            };
        }

        public PakFileReader(string filePath, VersionContainer? versions = null)
            : this(new FileInfo(filePath), versions) { }
        public PakFileReader(FileInfo file, VersionContainer? versions = null)
            : this(file.FullName, file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite), versions) { }
        public PakFileReader(string filePath, Stream stream, VersionContainer? versions = null)
            : this(new FStreamArchive(filePath, stream, versions)) { }
        public PakFileReader(string filePath, RandomAccessStream stream, VersionContainer? versions = null)
            : this(new FRandomAccessStreamArchive(filePath, stream, versions)) { }

        public override byte[] Extract(VfsEntry entry)
        {
            if (entry is not FPakEntry pakEntry || entry.Vfs != this) throw new ArgumentException($"�����pak�ļ���ȡ������Ҫ{entry.Vfs.Name}����Ϊ {Name}");
            // ����˶�ȡ������������ȡ�����򴴽�����ȡ���Ŀ�¡���ṩ�̰߳�ȫ
            var reader = IsConcurrent ? (FArchive)Ar.Clone() : Ar;
            if (pakEntry.IsCompressed)
            {
#if DEBUG
                Log.Debug("{EntryName} ʹ�� {CompressionMethod} ѹ��", pakEntry.Name, pakEntry.CompressionMethod);
#endif
                switch (Game)
                {
                    case EGame.GAME_�������� or EGame.GAME_�����ж�:
                        return NetEaseCompressedExtract(reader, pakEntry);
                    case EGame.GAME_��ƽ��Ӣ:
                        return GameForPeaceExtract(reader, pakEntry);
                    case EGame.GAME_Rennsport:
                        return RennsportCompressedExtract(reader, pakEntry);
                }

                var uncompressed = new byte[(int)pakEntry.UncompressedSize];
                var uncompressedOff = 0;
                foreach (var block in pakEntry.CompressionBlocks)
                {
                    var blockSize = (int)block.Size;
                    var srcSize = blockSize.Align(pakEntry.IsEncrypted ? Aes.ALIGN : 1);
                    // ��ȡѹ����
                    var compressed = ReadAndDecryptAt(block.CompressedStart, srcSize, reader, pakEntry.IsEncrypted);
                    // �����ѹ����С
                    // ��Ҫôֻ��ѹ�����С��
                    // Ҫô��������һ���飬����ʣ�����ݴ�С
                    var uncompressedSize = (int)Math.Min(pakEntry.CompressionBlockSize, pakEntry.UncompressedSize - uncompressedOff);
                    Decompress(compressed, 0, blockSize, uncompressed, uncompressedOff, uncompressedSize, pakEntry.CompressionMethod);
                    uncompressedOff += (int)pakEntry.CompressionBlockSize;
                }

                return uncompressed;
            }

            switch (Game)
            {
                case EGame.GAME_�������� or EGame.GAME_�����ж�:
                    return NetEaseExtract(reader, pakEntry);
                case EGame.GAME_Rennsport:
                    return RennsportExtract(reader, pakEntry);
            }

            // Pak��Ŀд���ļ�����֮ǰ��
            // �����������е���Ŀ��ͬ��ֻ��û������
            // ���ǲ���Ҫ�ٴ����л������� + file.StructSize
            var size = (int)pakEntry.UncompressedSize.Align(pakEntry.IsEncrypted ? Aes.ALIGN : 1);
            var data = ReadAndDecryptAt(pakEntry.Offset + pakEntry.StructSize /* ���ڽϾɵ�pak�汾�ƺ������������ */,
                size, reader, pakEntry.IsEncrypted);
            return size != pakEntry.UncompressedSize ? data.SubByteArray((int)pakEntry.UncompressedSize) : data;
        }

        public override void Mount(StringComparer pathComparer)
        {
            var watch = new Stopwatch();
            watch.Start();

            if (Info.Version >= PakFile_Version_PathHashIndex)
                ReadIndexUpdated(pathComparer);
            else if (Info.IndexIsFrozen)
                ReadFrozenIndex(pathComparer);
            else
                ReadIndexLegacy(pathComparer);

            if (!IsEncrypted && EncryptedFileCount > 0)
            {
                Log.Warning($"Pak�ļ�\"{Name}\"δ���ܵ����������ļ�");
            }

            if (Globals.LogVfsMounts)
            {
                var elapsed = watch.Elapsed;
                var sb = new StringBuilder($"Pak\"{Name}\":{FileCount} ���ļ�");
                if (EncryptedFileCount > 0)
                    sb.Append($" ({EncryptedFileCount} ������)");
                if (MountPoint.Contains("/"))
                    sb.Append($", ���ص�: \"{MountPoint}\"");
                sb.Append($", ��ȡ˳�� {ReadOrder}");
                sb.Append($", �汾 {(int)Info.Version}����ʱ {elapsed}");
                Log.Information(sb.ToString());
            }
        }

        private void ReadIndexLegacy(StringComparer pathComparer)
        {
            Ar.Position = Info.IndexOffset;
            var index = new FByteArchive($"{Name} - ����", ReadAndDecrypt((int)Info.IndexSize), Versions);

            string mountPoint;
            try
            {
                mountPoint = index.ReadFString();
            }
            catch (Exception e)
            {
                throw new InvalidAesKeyException($"������AES��Կ'{AesKey?.KeyString}' ��������'{Name}'", e);
            }

            ValidateMountPoint(ref mountPoint);
            MountPoint = mountPoint;

            if (Ar.Game == EGame.GAME_��ƽ��Ӣ)
            {
                GameForPeaceReadIndex(pathComparer, index);
                return;
            }

            var fileCount = index.Read<int>();
            var files = new Dictionary<string, GameFile>(fileCount, pathComparer);
            for (var i = 0; i < fileCount; i++)
            {
                var path = string.Concat(mountPoint, index.ReadFString());
                var entry = new FPakEntry(this, path, index);
                if (entry is { IsDeleted: true, Size: 0 }) continue;
                if (entry.IsEncrypted) EncryptedFileCount++;
                files[path] = entry;
            }

            Files = files;
        }

        private void ReadIndexUpdated(StringComparer pathComparer)
        {
            if (Ar.Game == EGame.GAME_����)
            {
                CoAReadIndexUpdated(pathComparer);
                return;
            }

            // ׼�����������ڱ�Ҫʱ����
            Ar.Position = Info.IndexOffset;
            using FArchive primaryIndex = new FByteArchive($"{Name} - Primary Index", ReadAndDecrypt((int)Info.IndexSize));

            int fileCount = 0;
            EncryptedFileCount = 0;

            if (Ar.Game is EGame.GAME_Ԫ��֮�� or EGame.GAME_�������ж�)
            {
                primaryIndex.Position += 8; // PathHashSeed
                fileCount = primaryIndex.Read<int>();
            }

            string mountPoint;
            try
            {
                mountPoint = primaryIndex.ReadFString();
            }
            catch (Exception e)
            {
                throw new InvalidAesKeyException($"������AES��Կ'{AesKey?.KeyString}' ��������'{Name}'", e);
            }

            ValidateMountPoint(ref mountPoint);
            MountPoint = mountPoint;

            if (!(Ar.Game is EGame.GAME_Ԫ��֮�� or EGame.GAME_�������ж�))
            {
                fileCount = primaryIndex.Read<int>();
                primaryIndex.Position += 8; // PathHashSeed
            }

            if (!primaryIndex.ReadBoolean())
                throw new ParserException(primaryIndex, "û��·����ϣ����");

            primaryIndex.Position += 36; // PathHashIndexOffset (long) + PathHashIndexSize (long) + PathHashIndexHash (20 bytes)
            if (Ar.Game == EGame.GAME_Rennsport) primaryIndex.Position += 16;

            if (!primaryIndex.ReadBoolean())
                throw new ParserException(primaryIndex, "û��Ŀ¼����");

            if (Ar.Game == EGame.GAME_ȫ������_���) primaryIndex.Position += 40; // �ظ���Ŀ

            var directoryIndexOffset = primaryIndex.Read<long>();
            var directoryIndexSize = primaryIndex.Read<long>();
            primaryIndex.Position += 20; // Ŀ¼������ϣ
            if (Ar.Game == EGame.GAME_Rennsport) primaryIndex.Position += 20;
            var encodedPakEntriesSize = primaryIndex.Read<int>();
            if (Ar.Game == EGame.GAME_Rennsport)
            {
                primaryIndex.Position -= 4;
                encodedPakEntriesSize = (int)(primaryIndex.Length - primaryIndex.Position - 6);
            }

            var encodedPakEntriesData = primaryIndex.ReadBytes(encodedPakEntriesSize);
            using var encodedPakEntries = new FByteArchive($"{Name} - Encoded Pak Entries", encodedPakEntriesData, Versions); // ת��ΪFArchive����

            var FilesNum = primaryIndex.Read<int>();
            if (FilesNum < 0)
                throw new ParserException("��⵽�𻵵�pak������");

            var NonEncodedEntries = primaryIndex.ReadArray(FilesNum, () => new FPakEntry(this, "", primaryIndex));

            // ��ȡFDirectoryIndex
            Ar.Position = directoryIndexOffset;
            var data = Ar.Game != EGame.GAME_Rennsport
                ? ReadAndDecrypt((int)directoryIndexSize)
                : RennsportAes.RennsportDecrypt(Ar.ReadBytes((int)directoryIndexSize), 0, (int)directoryIndexSize, true, this, true);
            using var directoryIndex = new FByteArchive($"{Name} - Directory Index", data, Versions); // ת��ΪFArchive����

            var files = new Dictionary<string, GameFile>(fileCount, pathComparer);

            const int poolLength = 256;
            var mountPointSpan = MountPoint.AsSpan();
            using var charsPool = SpanOwner<char>.Allocate(poolLength * 2);
            var charsSpan = charsPool.Span;
            var dirPoolSpan = charsSpan[..poolLength];
            var fileNamePoolSpan = charsSpan[poolLength..];
            var directoryIndexLength = directoryIndex.Read<int>();
            for (var dirIndex = 0; dirIndex < directoryIndexLength; dirIndex++)
            {
                var dir = directoryIndex.ReadFString();
                var dirDictLength = directoryIndex.Read<int>();

                for (var j = 0; j < dirDictLength; j++)
                {
                    var name = directoryIndex.ReadFString();
                    string path;
                    if (mountPoint.EndsWith('/') && dir.StartsWith('/'))
                        path = dir.Length == 1 ? string.Concat(mountPoint, name) : string.Concat(mountPoint, dir[1..], name);
                    else
                        path = string.Concat(mountPoint, dir, name);

                    var offset = directoryIndex.Read<int>();
                    if (offset == int.MinValue) continue;

                    FPakEntry entry;
                    if (offset >= 0)
                    {
                        encodedPakEntries.Position = offset; // ���ö�ȡλ��
                        entry = new FPakEntry(this, path, encodedPakEntries); // ʹ��FArchive���캯��
                    }
                    else
                    {
                        var index = -offset - 1;
                        if (index < 0 || index >= NonEncodedEntries.Length)
                        {
                            Log.Warning("��Ч�ķǱ���pak��Ŀ����{Index}��·��{Path}", index, path);
                            continue;
                        }

                        entry = NonEncodedEntries[index];
                        entry.Path = path;
                    }
                    if (entry.IsEncrypted) EncryptedFileCount++;
                    files[path] = entry;
                }
            }

            Files = files;
        }

        private void ReadFrozenIndex(StringComparer pathComparer)
        {
            this.Ar.Position = Info.IndexOffset;
            var Ar = new FMemoryImageArchive(new FByteArchive("FPakFileData", this.Ar.ReadBytes((int)Info.IndexSize)), 8);

            var mountPoint = Ar.ReadFString();
            ValidateMountPoint(ref mountPoint);
            MountPoint = mountPoint;

            var entries = Ar.ReadArray(() => new FPakEntry(this, Ar));

            // ��ȡ TMap<FString, TMap<FString, int32>>
            var index = Ar.ReadTMap(
                () => Ar.ReadFString(),
                () => Ar.ReadTMap(
                    () => Ar.ReadFString(),
                    () => Ar.Read<int>(),
                    16, 4
                ),
                16, 56
            );

            var files = new Dictionary<string, GameFile>(entries.Length, pathComparer);
            foreach (var (dir, dirContents) in index)
            {
                foreach (var (name, fileIndex) in dirContents)
                {
                    string path;
                    if (mountPoint.EndsWith('/') && dir.StartsWith('/'))
                        path = dir.Length == 1 ? string.Concat(mountPoint, name) : string.Concat(mountPoint, dir[1..], name);
                    else
                        path = string.Concat(mountPoint, dir, name);

                    var entry = entries[fileIndex];
                    entry.Path = path;

                    if (entry is { IsDeleted: true, Size: 0 }) continue;
                    if (entry.IsEncrypted) EncryptedFileCount++;
                    files[path] = entry;
                }
            }

            Files = files;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override byte[] ReadAndDecrypt(int length) => ReadAndDecrypt(length, Ar, IsEncrypted);

        public override byte[] MountPointCheckBytes()
        {
            var reader = IsConcurrent ? (FArchive)Ar.Clone() : Ar;
            reader.Position = Info.IndexOffset;
            var size = Math.Min((int)Info.IndexSize, 4 + MAX_MOUNTPOINT_TEST_LENGTH * 2);
            return reader.ReadBytes(size.Align(Aes.ALIGN));
        }

        public override void Dispose()
        {
            Ar.Dispose();
        }
    }
}