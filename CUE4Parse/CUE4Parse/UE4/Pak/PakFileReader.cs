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
                Log.Warning($"Pak文件\"{Name}\"的版本 {(int)Info.Version}不受支持");
            }
        }

        // 这些游戏使用版本 >= 12 来表示其自定义格式
        private bool UsingCustomPakVersion()
        {
            return Ar.Game switch
            {
                EGame.GAME_无限暖暖 or EGame.GAME_遇见造物主 or EGame.GAME_黎明杀机
                    or EGame.GAME_尘白禁区 or EGame.GAME_火炬之光_无限 or EGame.GAME_幻塔
                    or EGame.GAME_全境封锁_曙光 or EGame.GAME_QQ_这tm算游戏 or EGame.GAME_元梦之星 => true,
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
            if (entry is not FPakEntry pakEntry || entry.Vfs != this) throw new ArgumentException($"错误的pak文件读取器，需要{entry.Vfs.Name}，此为 {Name}");
            // 如果此读取器用作并发读取器，则创建主读取器的克隆以提供线程安全
            var reader = IsConcurrent ? (FArchive)Ar.Clone() : Ar;
            if (pakEntry.IsCompressed)
            {
#if DEBUG
                Log.Debug("{EntryName} 使用 {CompressionMethod} 压缩", pakEntry.Name, pakEntry.CompressionMethod);
#endif
                switch (Game)
                {
                    case EGame.GAME_漫威争锋 or EGame.GAME_天启行动:
                        return NetEaseCompressedExtract(reader, pakEntry);
                    case EGame.GAME_和平精英:
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
                    // 读取压缩块
                    var compressed = ReadAndDecryptAt(block.CompressedStart, srcSize, reader, pakEntry.IsEncrypted);
                    // 计算解压缩大小
                    // 它要么只是压缩块大小，
                    // 要么如果是最后一个块，则是剩余数据大小
                    var uncompressedSize = (int)Math.Min(pakEntry.CompressionBlockSize, pakEntry.UncompressedSize - uncompressedOff);
                    Decompress(compressed, 0, blockSize, uncompressed, uncompressedOff, uncompressedSize, pakEntry.CompressionMethod);
                    uncompressedOff += (int)pakEntry.CompressionBlockSize;
                }

                return uncompressed;
            }

            switch (Game)
            {
                case EGame.GAME_漫威争锋 or EGame.GAME_天启行动:
                    return NetEaseExtract(reader, pakEntry);
                case EGame.GAME_Rennsport:
                    return RennsportExtract(reader, pakEntry);
            }

            // Pak条目写在文件数据之前，
            // 但它与索引中的条目相同，只是没有名称
            // 我们不需要再次序列化，所以 + file.StructSize
            var size = (int)pakEntry.UncompressedSize.Align(pakEntry.IsEncrypted ? Aes.ALIGN : 1);
            var data = ReadAndDecryptAt(pakEntry.Offset + pakEntry.StructSize /* 对于较旧的pak版本似乎不是这种情况 */,
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
                Log.Warning($"Pak文件\"{Name}\"未加密但包含加密文件");
            }

            if (Globals.LogVfsMounts)
            {
                var elapsed = watch.Elapsed;
                var sb = new StringBuilder($"Pak\"{Name}\":{FileCount} 个文件");
                if (EncryptedFileCount > 0)
                    sb.Append($" ({EncryptedFileCount} 个加密)");
                if (MountPoint.Contains("/"))
                    sb.Append($", 挂载点: \"{MountPoint}\"");
                sb.Append($", 读取顺序 {ReadOrder}");
                sb.Append($", 版本 {(int)Info.Version}，耗时 {elapsed}");
                Log.Information(sb.ToString());
            }
        }

        private void ReadIndexLegacy(StringComparer pathComparer)
        {
            Ar.Position = Info.IndexOffset;
            var index = new FByteArchive($"{Name} - 索引", ReadAndDecrypt((int)Info.IndexSize), Versions);

            string mountPoint;
            try
            {
                mountPoint = index.ReadFString();
            }
            catch (Exception e)
            {
                throw new InvalidAesKeyException($"给定的AES密钥'{AesKey?.KeyString}' 不适用于'{Name}'", e);
            }

            ValidateMountPoint(ref mountPoint);
            MountPoint = mountPoint;

            if (Ar.Game == EGame.GAME_和平精英)
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
            if (Ar.Game == EGame.GAME_晶核)
            {
                CoAReadIndexUpdated(pathComparer);
                return;
            }

            // 准备主索引并在必要时解密
            Ar.Position = Info.IndexOffset;
            using FArchive primaryIndex = new FByteArchive($"{Name} - Primary Index", ReadAndDecrypt((int)Info.IndexSize));

            int fileCount = 0;
            EncryptedFileCount = 0;

            if (Ar.Game is EGame.GAME_元梦之星 or EGame.GAME_三角洲行动)
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
                throw new InvalidAesKeyException($"给定的AES密钥'{AesKey?.KeyString}' 不适用于'{Name}'", e);
            }

            ValidateMountPoint(ref mountPoint);
            MountPoint = mountPoint;

            if (!(Ar.Game is EGame.GAME_元梦之星 or EGame.GAME_三角洲行动))
            {
                fileCount = primaryIndex.Read<int>();
                primaryIndex.Position += 8; // PathHashSeed
            }

            if (!primaryIndex.ReadBoolean())
                throw new ParserException(primaryIndex, "没有路径哈希索引");

            primaryIndex.Position += 36; // PathHashIndexOffset (long) + PathHashIndexSize (long) + PathHashIndexHash (20 bytes)
            if (Ar.Game == EGame.GAME_Rennsport) primaryIndex.Position += 16;

            if (!primaryIndex.ReadBoolean())
                throw new ParserException(primaryIndex, "没有目录索引");

            if (Ar.Game == EGame.GAME_全境封锁_曙光) primaryIndex.Position += 40; // 重复条目

            var directoryIndexOffset = primaryIndex.Read<long>();
            var directoryIndexSize = primaryIndex.Read<long>();
            primaryIndex.Position += 20; // 目录索引哈希
            if (Ar.Game == EGame.GAME_Rennsport) primaryIndex.Position += 20;
            var encodedPakEntriesSize = primaryIndex.Read<int>();
            if (Ar.Game == EGame.GAME_Rennsport)
            {
                primaryIndex.Position -= 4;
                encodedPakEntriesSize = (int)(primaryIndex.Length - primaryIndex.Position - 6);
            }

            var encodedPakEntriesData = primaryIndex.ReadBytes(encodedPakEntriesSize);
            using var encodedPakEntries = new FByteArchive($"{Name} - Encoded Pak Entries", encodedPakEntriesData, Versions); // 转换为FArchive类型

            var FilesNum = primaryIndex.Read<int>();
            if (FilesNum < 0)
                throw new ParserException("检测到损坏的pak主索引");

            var NonEncodedEntries = primaryIndex.ReadArray(FilesNum, () => new FPakEntry(this, "", primaryIndex));

            // 读取FDirectoryIndex
            Ar.Position = directoryIndexOffset;
            var data = Ar.Game != EGame.GAME_Rennsport
                ? ReadAndDecrypt((int)directoryIndexSize)
                : RennsportAes.RennsportDecrypt(Ar.ReadBytes((int)directoryIndexSize), 0, (int)directoryIndexSize, true, this, true);
            using var directoryIndex = new FByteArchive($"{Name} - Directory Index", data, Versions); // 转换为FArchive类型

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
                        encodedPakEntries.Position = offset; // 设置读取位置
                        entry = new FPakEntry(this, path, encodedPakEntries); // 使用FArchive构造函数
                    }
                    else
                    {
                        var index = -offset - 1;
                        if (index < 0 || index >= NonEncodedEntries.Length)
                        {
                            Log.Warning("无效的非编码pak条目索引{Index}，路径{Path}", index, path);
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

            // 读取 TMap<FString, TMap<FString, int32>>
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