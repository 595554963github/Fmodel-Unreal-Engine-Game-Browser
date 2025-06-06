using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;

namespace CUE4Parse.UE4.IO
{
    public class IoStoreOnDemandReader : IoStoreReader
    {
        public readonly FOnDemandTocEntry[] Entries;

        private readonly IoStoreOnDemandDownloader _downloader;

        public IoStoreOnDemandReader(FArchive tocStream, FOnDemandTocEntry[] entries, IoStoreOnDemandDownloader downloader)
            : base(tocStream, it => new FByteArchive(it, Array.Empty<byte>(), tocStream.Versions))
        {
            Entries = entries;
            _downloader = downloader;
        }

        public override byte[] Extract(VfsEntry entry)
        {
            if (!(entry is FIoStoreEntry ioEntry) || entry.Vfs != this) throw new ArgumentException($"错误的IO存储读取器，需要 {entry.Vfs.Path}，此为 {Path}");
            return Read(Entries[ioEntry.TocEntryIndex]);
        }

        public override byte[] Read(FIoChunkId chunkId) => Read(Entries.FirstOrDefault(entry => entry.ChunkId == chunkId));

        private byte[] Read(FOnDemandTocEntry? onDemandEntry)
        {
            if (onDemandEntry == null) throw new ParserException("无法读取未知的按需条目");
            if (TryResolve(onDemandEntry.ChunkId, out var offsetLength))
            {
                return Read(onDemandEntry.Hash.ToString().ToLower(), (long)offsetLength.Offset, (long)offsetLength.Length);
            }
            throw new KeyNotFoundException($"在按需IO存储 {Name} 中找不到块 {onDemandEntry.ChunkId}");
        }

        private byte[] Read(string hash, long offset, long length)
        {
            var reader = _downloader.Download($"chunks/{hash[..2]}/{hash}.iochunk").GetAwaiter().GetResult();

            var compressionBlockSize = TocResource.Header.CompressionBlockSize;
            var dst = new byte[length];
            var firstBlockIndex = (int)(offset / compressionBlockSize);
            var lastBlockIndex = (int)(((offset + dst.Length).Align((int)compressionBlockSize) - 1) / compressionBlockSize);
            var offsetInBlock = offset % compressionBlockSize;
            var remainingSize = length;
            var dstOffset = 0;

            var compressedBuffer = Array.Empty<byte>();
            var uncompressedBuffer = Array.Empty<byte>();

            for (int blockIndex = firstBlockIndex; blockIndex <= lastBlockIndex; blockIndex++)
            {
                ref var compressionBlock = ref TocResource.CompressionBlocks[blockIndex];

                var rawSize = compressionBlock.CompressedSize.Align(Aes.ALIGN);
                if (compressedBuffer.Length < rawSize)
                {
                    compressedBuffer = new byte[rawSize];
                }

                var uncompressedSize = compressionBlock.UncompressedSize;
                if (uncompressedBuffer.Length < uncompressedSize)
                {
                    uncompressedBuffer = new byte[uncompressedSize];
                }

                ReadFully(reader, compressedBuffer, 0, (int)rawSize);
                compressedBuffer = DecryptIfEncrypted(compressedBuffer, 0, (int)rawSize);

                byte[] src;
                if (compressionBlock.CompressionMethodIndex == 0)
                {
                    src = compressedBuffer;
                }
                else
                {
                    var compressionMethod = TocResource.CompressionMethods[compressionBlock.CompressionMethodIndex];
                    Compression.Compression.Decompress(compressedBuffer, 0, (int)rawSize, uncompressedBuffer, 0, (int)uncompressedSize, compressionMethod);
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

        private static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
                int read = stream.Read(buffer, offset + bytesRead, count - bytesRead);
                if (read == 0)
                {
                    throw new EndOfStreamException("未能读取所需的字节数，提前到达流的末尾");
                }
                bytesRead += read;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _downloader.Dispose();
        }
    }
}