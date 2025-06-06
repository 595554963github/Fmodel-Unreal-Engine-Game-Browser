using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using GenericReader;
using OffiUtils;

namespace CUE4Parse.UE4.IO;

public partial class IoStoreReader : AbstractAesVfsReader
{
    public readonly IReadOnlyList<FArchive> ContainerStreams;

    public readonly FIoStoreTocResource TocResource;
    public readonly Dictionary<FIoChunkId, FIoOffsetAndLength>? TocImperfectHashMapFallback;
    public FIoContainerHeader? ContainerHeader { get; private set; }

    public override string MountPoint { get; protected set; } = string.Empty;
    public sealed override long Length { get; set; }

    public override bool HasDirectoryIndex => TocResource.DirectoryIndexBuffer != null;
    public override FGuid EncryptionKeyGuid => TocResource.Header.EncryptionKeyGuid;
    public override bool IsEncrypted => TocResource.Header.ContainerFlags.HasFlag(EIoContainerFlags.Encrypted);

    public IoStoreReader(string tocPath, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex, VersionContainer? versions = null)
        : this(new FileInfo(tocPath), readOptions, versions) { }
    public IoStoreReader(FileInfo utocFile, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex, VersionContainer? versions = null)
        : this(new FByteArchive(utocFile.FullName, File.ReadAllBytes(utocFile.FullName), versions), it => new FStreamArchive(it, File.Open(it, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), versions), readOptions) { }
    public IoStoreReader(string tocPath, Stream tocStream, Stream casStream, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex, VersionContainer? versions = null)
        : this(new FStreamArchive(tocPath, tocStream, versions), it => new FStreamArchive(it, casStream, versions), readOptions) { }
    public IoStoreReader(string tocPath, Stream tocStream, Func<string, FArchive> openContainerStreamFunc, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex, VersionContainer? versions = null)
        : this(new FStreamArchive(tocPath, tocStream, versions), openContainerStreamFunc, readOptions) { }

    public IoStoreReader(string tocPath, RandomAccessStream tocStream, RandomAccessStream casStream, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex, VersionContainer? versions = null)
        : this(new FRandomAccessStreamArchive(tocPath, tocStream, versions), it => new FRandomAccessStreamArchive(it, casStream, versions), readOptions) { }
    public IoStoreReader(string tocPath, RandomAccessStream tocStream, Func<string, FRandomAccessStreamArchive> openContainerStreamFunc, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex, VersionContainer? versions = null)
        : this(new FRandomAccessStreamArchive(tocPath, tocStream, versions), openContainerStreamFunc, readOptions) { }

    public IoStoreReader(FArchive tocStream, Func<string, FArchive> openContainerStreamFunc, EIoStoreTocReadOptions readOptions = EIoStoreTocReadOptions.ReadDirectoryIndex)
        : base(tocStream.Name, tocStream.Versions)
    {
        Length = tocStream.Length;
        TocResource = new FIoStoreTocResource(tocStream, readOptions);

        List<FArchive> containerStreams;
        if (TocResource.Header.PartitionCount <= 1)
        {
            containerStreams = new List<FArchive>(1);
            try
            {
                containerStreams.Add(openContainerStreamFunc(tocStream.Name.SubstringBeforeLast('.') + ".ucas"));
            }
            catch (Exception e)
            {
                throw new FIoStatusException(EIoErrorCode.FileOpenFailed, $"无法为{tocStream.Name} 打开容器分区0", e);
            }
        }
        else
        {
            containerStreams = new List<FArchive>((int)TocResource.Header.PartitionCount);
            var environmentPath = tocStream.Name.SubstringBeforeLast('.');
            for (int i = 0; i < TocResource.Header.PartitionCount; i++)
            {
                try
                {
                    var path = i > 0 ? string.Concat(environmentPath, "_s", i, ".ucas") : string.Concat(environmentPath, ".ucas");
                    containerStreams.Add(openContainerStreamFunc(path));
                }
                catch (Exception e)
                {
                    throw new FIoStatusException(EIoErrorCode.FileOpenFailed, $"无法为{tocStream.Name}打开容器分区 {i}", e);
                }
            }
        }

        Length += containerStreams.Sum(x => x.Length);
        ContainerStreams = containerStreams;
        if (TocResource.ChunkPerfectHashSeeds != null)
        {
            TocImperfectHashMapFallback = new();
            if (TocResource.ChunkIndicesWithoutPerfectHash != null)
            {
                foreach (var chunkIndexWithoutPerfectHash in TocResource.ChunkIndicesWithoutPerfectHash)
                {
                    TocImperfectHashMapFallback[TocResource.ChunkIds[chunkIndexWithoutPerfectHash]] = TocResource.ChunkOffsetLengths[chunkIndexWithoutPerfectHash];
                }
            }
        }
#if GENERATE_CHUNK_ID_DICT
            else
            {
                TocImperfectHashMapFallback = new Dictionary<FIoChunkId, FIoOffsetAndLength>((int) TocResource.Header.TocEntryCount);
                for (var i = 0; i < TocResource.ChunkIds.Length; i++)
                {
                    TocImperfectHashMapFallback[TocResource.ChunkIds[i]] = TocResource.ChunkOffsetLengths[i];
                }
            }
#endif
        if (TocResource.Header.Version > EIoStoreTocVersion.Latest)
        {
            Log.Warning("Io 存储\"{0}\"具有不支持的版本{1}", Path, (int)TocResource.Header.Version);
        }
    }

    public override byte[] Extract(VfsEntry entry)
    {
        if (!(entry is FIoStoreEntry ioEntry) || entry.Vfs != this) throw new ArgumentException($"错误的IO存储读取器，需要 {entry.Vfs.Path}，此为{Path}");
        return Read(ioEntry.Offset, ioEntry.Size);
    }

    public bool DoesChunkExist(FIoChunkId chunkId) => TryResolve(chunkId, out _);

    public bool TryResolve(FIoChunkId chunkId, out FIoOffsetAndLength outOffsetLength)
    {
        if (TocResource.ChunkPerfectHashSeeds != null)
        {
            var chunkCount = TocResource.Header.TocEntryCount;
            if (chunkCount == 0)
            {
                outOffsetLength = default;
                return false;
            }
            var seedCount = (uint)TocResource.ChunkPerfectHashSeeds.Length;
            var seedIndex = (uint)(chunkId.HashWithSeed(0) % seedCount);
            var seed = TocResource.ChunkPerfectHashSeeds[seedIndex];
            if (seed == 0)
            {
                outOffsetLength = default;
                return false;
            }
            uint slot;
            if (seed < 0)
            {
                var seedAsIndex = (uint)(-seed - 1);
                if (seedAsIndex < chunkCount)
                {
                    slot = seedAsIndex;
                }
                else
                {
                    // 没有完美哈希的条目
                    return TryResolveImperfect(chunkId, out outOffsetLength);
                }
            }
            else
            {
                slot = (uint)(chunkId.HashWithSeed(seed) % chunkCount);
            }
            if (TocResource.ChunkIds[slot].GetHashCode() == chunkId.GetHashCode())
            {
                outOffsetLength = TocResource.ChunkOffsetLengths[slot];
                return true;
            }
            outOffsetLength = default;
            return false;
        }

        return TryResolveImperfect(chunkId, out outOffsetLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryResolveImperfect(FIoChunkId chunkId, out FIoOffsetAndLength outOffsetLength)
    {
        if (TocImperfectHashMapFallback != null)
        {
            return TocImperfectHashMapFallback.TryGetValue(chunkId, out outOffsetLength);
        }

        var chunkIndex = Array.IndexOf(TocResource.ChunkIds, chunkId);
        if (chunkIndex == -1)
        {
            outOffsetLength = default;
            return false;
        }

        outOffsetLength = TocResource.ChunkOffsetLengths[chunkIndex];
        return true;
    }

    public virtual byte[] Read(FIoChunkId chunkId)
    {
        if (TryResolve(chunkId, out var offsetLength))
        {
            return Read((long)offsetLength.Offset, (long)offsetLength.Length);
        }

        throw new KeyNotFoundException($"在IoStore {Name} 中找不到块 {chunkId}");
    }

    private byte[] Read(long offset, long length)
    {
        var compressionBlockSize = TocResource.Header.CompressionBlockSize;
        var dst = new byte[length];
        var firstBlockIndex = (int)(offset / compressionBlockSize);
        var lastBlockIndex = (int)(((offset + dst.Length).Align((int)compressionBlockSize) - 1) / compressionBlockSize);
        var offsetInBlock = offset % compressionBlockSize;
        var remainingSize = length;
        var dstOffset = 0;

        var compressedBuffer = Array.Empty<byte>();
        var uncompressedBuffer = Array.Empty<byte>();

        FArchive?[]? clonedReaders = null;

        for (int blockIndex = firstBlockIndex; blockIndex <= lastBlockIndex; blockIndex++)
        {
            ref var compressionBlock = ref TocResource.CompressionBlocks[blockIndex];

            var rawSize = compressionBlock.CompressedSize.Align(Aes.ALIGN);
            if (compressedBuffer.Length < rawSize)
            {
                compressedBuffer = new byte[rawSize];
            }

            var partitionIndex = (int)((ulong)compressionBlock.Offset / TocResource.Header.PartitionSize);
            var partitionOffset = (long)((ulong)compressionBlock.Offset % TocResource.Header.PartitionSize);
            FArchive reader;
            if (IsConcurrent)
            {
                clonedReaders ??= new FArchive?[ContainerStreams.Count];
                ref var clone = ref clonedReaders[partitionIndex];
                clone ??= (FArchive)ContainerStreams[partitionIndex].Clone();
                reader = clone;
            }
            else reader = ContainerStreams[partitionIndex];

            reader.ReadAt(partitionOffset, compressedBuffer, 0, (int)rawSize);
            compressedBuffer = DecryptIfEncrypted(compressedBuffer, 0, (int)rawSize, IsEncrypted, Game == EGame.GAME_界外狂潮 && Path.Contains("global", StringComparison.Ordinal));

            byte[] src;
            if (compressionBlock.CompressionMethodIndex == 0)
            {
                src = compressedBuffer;
            }
            else
            {
                var uncompressedSize = compressionBlock.UncompressedSize;
                if (uncompressedBuffer.Length < uncompressedSize)
                {
                    uncompressedBuffer = new byte[uncompressedSize];
                }

                var compressionMethod = TocResource.CompressionMethods[compressionBlock.CompressionMethodIndex];
                Compression.Compression.Decompress(compressedBuffer, 0, (int)compressionBlock.CompressedSize, uncompressedBuffer, 0,
                    (int)uncompressedSize, compressionMethod, reader);
                src = uncompressedBuffer;
            }

            var sizeInBlock = (int)Math.Min(compressionBlockSize - offsetInBlock, remainingSize);
            Buffer.BlockCopy(src, (int)offsetInBlock, dst, dstOffset, sizeInBlock);
            offsetInBlock = 0;
            remainingSize -= sizeInBlock;
            dstOffset += sizeInBlock;
        }

        return dst;
    }

    public override void Mount(StringComparer pathComparer)
    {
        var watch = new Stopwatch();
        watch.Start();

        ProcessIndex(pathComparer);
        if (Game >= EGame.GAME_UE5_0)
        {
            ContainerHeader = ReadContainerHeader();
        }

        if (Globals.LogVfsMounts)
        {
            var elapsed = watch.Elapsed;
            var sb = new StringBuilder($"IoStore\"{Name}\": {FileCount}个文件");
            if (EncryptedFileCount > 0)
                sb.Append($" ({EncryptedFileCount} 个已加密)");
            if (MountPoint.Contains("/"))
                sb.Append($", 挂载点: \"{MountPoint}\"");
            sb.Append($", 顺序 {ReadOrder}");
            sb.Append($", 版本 {(int)TocResource.Header.Version}，耗时 {elapsed}");
            Log.Information(sb.ToString());
        }
    }

    private void ProcessIndex(StringComparer pathComparer)
    {
        if (!HasDirectoryIndex || TocResource.DirectoryIndexBuffer == null) throw new ParserException("没有目录索引");
        if (Game == EGame.GAME_Brickadia)
        {
            GenerateBrickadiaIndex(pathComparer);
            return;
        }

        using var directoryIndex = new GenericBufferReader(DecryptIfEncrypted(TocResource.DirectoryIndexBuffer));

        string mountPoint;
        try
        {
            mountPoint = directoryIndex.ReadFString();
        }
        catch (Exception e)
        {
            throw new InvalidAesKeyException($"给定的aes密钥'{AesKey?.KeyString}'不适用于'{Path}'", e);
        }

        ValidateMountPoint(ref mountPoint);
        MountPoint = mountPoint;

        var directoryEntries = directoryIndex.ReadArray<FIoDirectoryIndexEntry>();
        var fileEntries = directoryIndex.ReadArray<FIoFileIndexEntry>();
        var stringTable = directoryIndex.ReadFStringMemoryArray();

        var files = new Dictionary<string, GameFile>(fileEntries.Length, pathComparer);
        var dirNamePool = ArrayPool<char>.Shared.Rent(256);
        var currentLength = WriteToBuffer(dirNamePool, 0, MountPoint);
        ReadIndex(dirNamePool, currentLength, 0U);

        void ReadIndex(char[] directoryName, int directoryLength, uint dir)
        {
            const uint invalidHandle = uint.MaxValue;
            while (dir != invalidHandle)
            {
                var dirEntry = directoryEntries[dir];
                var dirName = dirEntry.Name != invalidHandle ? stringTable[dirEntry.Name].ToString() : default;
                var directoryLengthSnapshot = directoryLength;
                if (!string.IsNullOrEmpty(dirName))
                    directoryLength = WriteToBuffer(directoryName, directoryLength, dirName, false);

                var file = dirEntry.FirstFileEntry;
                while (file != invalidHandle)
                {
                    var fileEntry = fileEntries[file];
                    var name = stringTable[fileEntry.Name].ToString();
                    var fullPathLength = WriteToBuffer(directoryName, directoryLength, name, true);
                    var fullPathSpan = directoryName.AsSpan(..fullPathLength);
                    var path = new string(fullPathSpan);

                    var entry = new FIoStoreEntry(this, path, fileEntry.UserData);
                    if (entry.IsEncrypted) EncryptedFileCount++;
                    files[path] = entry;

                    file = fileEntry.NextFileEntry;
                }

                ReadIndex(directoryName, directoryLength, dirEntry.FirstChildEntry);
                dir = dirEntry.NextSiblingEntry;
                directoryLength = directoryLengthSnapshot;
            }
        }

        Files = files;
        ArrayPool<char>.Shared.Return(dirNamePool);
    }

    private int WriteToBuffer(char[] buffer, int offset, string value, bool addSeparator = true)
    {
        if (addSeparator && offset > 0 && buffer[offset - 1] != '/')
        {
            buffer[offset++] = '/';
        }

        foreach (var c in value)
        {
            buffer[offset++] = c;
        }

        return offset;
    }

    private FIoContainerHeader ReadContainerHeader()
    {
        var headerChunkId = new FIoChunkId(TocResource.Header.ContainerId.Id, 0, Game >= EGame.GAME_UE5_0 ? (byte)EIoChunkType5.ContainerHeader : (byte)EIoChunkType.ContainerHeader);
        var Ar = new FByteArchive("ContainerHeader", Read(headerChunkId), Versions);
        return new FIoContainerHeader(Ar);
    }

    public override byte[] MountPointCheckBytes() => TocResource.DirectoryIndexBuffer ?? new byte[MAX_MOUNTPOINT_TEST_LENGTH];
    protected override byte[] ReadAndDecrypt(int length) => throw new InvalidOperationException("IoStore无法在没有上下文的情况下读取字节");

    public override void Dispose()
    {
        foreach (var stream in ContainerStreams)
        {
            stream.Dispose();
        }
    }
}