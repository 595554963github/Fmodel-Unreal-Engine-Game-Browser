using System;
using System.Diagnostics;
using System.IO;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using static CUE4Parse.Compression.Compression;
using static CUE4Parse.UE4.Objects.Core.Misc.ECompressionFlags;

namespace CUE4Parse.UE4.Readers
{
    public class FArchiveLoadCompressedProxy : FArchive
    {
        private readonly byte[] _compressedData;
        private int _currentIndex;
        private readonly byte[] _tmpData;
        private int _tmpDataPos;
        private int _tmpDataSize;
        private bool _shouldSerializeFromArray;
        private long _rawBytesSerialized;
        private readonly string _compressionFormat;
        private readonly ECompressionFlags _compressionFlags;

        public FArchiveLoadCompressedProxy(string name, byte[] compressedData, string compressionFormat, ECompressionFlags flags = COMPRESS_None, VersionContainer? versions = null) : base(versions)
        {
            Name = name;
            _compressedData = compressedData;
            _compressionFormat = compressionFormat;
            _compressionFlags = flags;

            _tmpData = new byte[LOADING_COMPRESSION_CHUNK_SIZE];
            _tmpDataPos = LOADING_COMPRESSION_CHUNK_SIZE;
            _tmpDataSize = LOADING_COMPRESSION_CHUNK_SIZE;
        }

        public override string Name { get; }

        public override object Clone() => new FArchiveLoadCompressedProxy(Name, _compressedData, _compressionFormat, _compressionFlags, Versions);

        public override int Read(byte[]? dstData, int offset, int count)
        {
            if (dstData == null)
            {
                // Handle seeking case where we don't need to copy data
                return SeekRead(count);
            }

            if (offset < 0 || offset >= dstData.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "偏移量超出目标数组范围");
            }

            if (count < 0 || offset + count > dstData.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "读取长度超出目标数组范围");
            }

            if (_shouldSerializeFromArray)
            {
                // SerializedCompressed reads the compressed data from here
                if (_currentIndex + count > _compressedData.Length)
                {
                    throw new IOException($"尝试读取超出压缩数据范围的字节，当前索引{_currentIndex}，请求长度 {count}，总长度{_compressedData.Length}");
                }

                Buffer.BlockCopy(_compressedData, _currentIndex, dstData, offset, count);
                _currentIndex += count;
                return count;
            }

            // Regular call to serialize, read from temp buffer
            var dstPos = offset;
            var remainingCount = count;

            while (remainingCount > 0)
            {
                var bytesToCopy = Math.Min(remainingCount, _tmpDataSize - _tmpDataPos);

                // Enough room in buffer to copy some data
                if (bytesToCopy > 0)
                {
                    Buffer.BlockCopy(_tmpData, _tmpDataPos, dstData, dstPos, bytesToCopy);
                    dstPos += bytesToCopy;
                    remainingCount -= bytesToCopy;
                    _tmpDataPos += bytesToCopy;
                    _rawBytesSerialized += bytesToCopy;
                }
                // Tmp buffer fully exhausted, decompress new one
                else
                {
                    // Decompress more data
                    DecompressMoreData();

                    if (_tmpDataSize == 0)
                    {
                        // Wanted more but couldn't get any, avoid infinite loop
                        throw new ParserException("无法解压更多数据");
                    }
                }
            }

            return count;
        }

        private int SeekRead(int count)
        {
            var remainingCount = count;

            while (remainingCount > 0)
            {
                var bytesToSkip = Math.Min(remainingCount, _tmpDataSize - _tmpDataPos);

                if (bytesToSkip > 0)
                {
                    remainingCount -= bytesToSkip;
                    _tmpDataPos += bytesToSkip;
                    _rawBytesSerialized += bytesToSkip;
                }
                else
                {
                    DecompressMoreData();

                    if (_tmpDataSize == 0)
                    {
                        throw new ParserException("无法解压更多数据");
                    }
                }
            }

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Trace.Assert(origin == SeekOrigin.Begin);
            var currentPos = Position;
            var difference = offset - currentPos;

            // We only support forward seeking
            if (difference < 0)
            {
                throw new IOException("仅支持向前查找");
            }

            // Seek by serializing data with NULL destination
            SeekRead((int)difference);
            return Position;
        }

        public override bool CanSeek => true;
        public override long Length => throw new InvalidOperationException("不支持获取长度");
        public override long Position
        {
            get => _rawBytesSerialized;
            set => Seek(value, SeekOrigin.Begin);
        }

        private void DecompressMoreData()
        {
            // This will call Serialize so we need to indicate that we want to serialize from array
            _shouldSerializeFromArray = true;
            SerializeCompressedNew(_tmpData, LOADING_COMPRESSION_CHUNK_SIZE, _compressionFormat, _compressionFlags, false, out var decompressedLength);

            // Last chunk will be partial:
            // All chunks before last should have size == LOADING_COMPRESSION_CHUNK_SIZE
            Trace.Assert(decompressedLength <= LOADING_COMPRESSION_CHUNK_SIZE);

            _shouldSerializeFromArray = false;
            // Buffer is filled again, reset
            _tmpDataPos = 0;
            _tmpDataSize = (int)decompressedLength;
        }
    }
}