using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;

using K4os.Compression.LZ4;

using ZstdSharp;

namespace CUE4Parse.Compression
{
    public static class Compression
    {
        public const int LOADING_COMPRESSION_CHUNK_SIZE = 131072;

        public static byte[] Decompress(byte[] compressed, int uncompressedSize, CompressionMethod method, FArchive? reader = null) =>
            Decompress(compressed, 0, compressed.Length, uncompressedSize, method, reader);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decompress(byte[] compressed, int compressedOffset, int compressedCount, int uncompressedSize, CompressionMethod method, FArchive? reader = null)
        {
            var uncompressed = new byte[uncompressedSize];
            Decompress(compressed, compressedOffset, compressedCount, uncompressed, 0, uncompressedSize, method);
            return uncompressed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decompress(byte[] compressed, byte[] dst, CompressionMethod method, FArchive? reader = null) =>
            Decompress(compressed, 0, compressed.Length, dst, 0, dst.Length, method, reader);
        public static void Decompress(byte[] compressed, int compressedOffset, int compressedSize, byte[] uncompressed, int uncompressedOffset, int uncompressedSize, CompressionMethod method, FArchive? reader = null)
        {
            var srcStream = new MemoryStream(compressed, compressedOffset, compressedSize, false);
            switch (method)
            {
                case CompressionMethod.None:
                    Buffer.BlockCopy(compressed, compressedOffset, uncompressed, uncompressedOffset, compressedSize);
                    return;
                case CompressionMethod.Zlib:
                    ZlibHelper.Decompress(compressed, compressedOffset, compressedSize, uncompressed, uncompressedOffset, uncompressedSize, reader);
                    return;
                case CompressionMethod.Gzip:
                    using (var gzip = new GZipStream(srcStream, CompressionMode.Decompress))
                    {
                        int bytesRead;
                        while ((bytesRead = gzip.Read(uncompressed, uncompressedOffset, uncompressedSize)) > 0)
                        {
                            uncompressedOffset += bytesRead;
                            uncompressedSize -= bytesRead;
                        }
                    }
                    return;
                case CompressionMethod.Oodle:
                    OodleHelper.Decompress(compressed, compressedOffset, compressedSize, uncompressed, uncompressedOffset, uncompressedSize, reader);
                    return;
                case CompressionMethod.LZ4:
                    var uncompressedBuffer = new byte[uncompressedSize + uncompressedSize / 255 + 16]; // LZ4_compressBound(uncompressedSize)
                    int result;
#if USE_LZ4_NATIVE_LIB
                    unsafe
                    {
                        fixed (byte* compressedPtr = compressed, uncompressedBufferPtr = uncompressedBuffer)
                        {
                            result = LZ4.LZ4_decompress_safe(compressedPtr + compressedOffset, uncompressedBufferPtr, compressedSize, uncompressedBuffer.Length);
                        }
                    }
#else
                    result = LZ4Codec.Decode(compressed, compressedOffset, compressedSize, uncompressedBuffer, 0, uncompressedBuffer.Length);
#endif
                    Buffer.BlockCopy(uncompressedBuffer, 0, uncompressed, uncompressedOffset, uncompressedSize);
                    if (result != uncompressedSize) throw new FileLoadException($"��ѹLZ4����ʧ��(Ԥ��: {uncompressedSize}, ���: {result})");
                    return;
                case CompressionMethod.Zstd:
                    {
                        using (var compressionStream = new DecompressionStream(srcStream))
                        {
                            int bytesRead;
                            while ((bytesRead = compressionStream.Read(uncompressed, uncompressedOffset, uncompressedSize)) > 0)
                            {
                                uncompressedOffset += bytesRead;
                                uncompressedSize -= bytesRead;
                            }
                        }
                        return;
                    }
                default:
                    if (reader != null) throw new UnknownCompressionMethodException(reader, $"ѹ����\"{method}\"��δ֪��");
                    else throw new UnknownCompressionMethodException($"ѹ����\"{method}\"��δ֪��");
            }
        }
    }
}