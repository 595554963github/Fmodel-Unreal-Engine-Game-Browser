using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Exceptions;
using Newtonsoft.Json;
using Serilog;
using static CUE4Parse.UE4.Assets.Objects.EBulkDataFlags;

namespace CUE4Parse.UE4.Assets.Objects
{
    [JsonConverter(typeof(FByteBulkDataConverter))]
    public class FByteBulkData
    {
        public static bool LazyLoad = true;

        public readonly FByteBulkDataHeader Header;
        public EBulkDataFlags BulkDataFlags => Header.BulkDataFlags;

        public byte[]? Data => _data?.Value;
        private readonly Lazy<byte[]?>? _data;

        private readonly FAssetArchive? _savedAr;
        private readonly long _dataPosition;

        public FByteBulkData(byte[] data)
        {
            _data = new Lazy<byte[]?>(() => data);
        }

        public FByteBulkData(Lazy<byte[]?> data)
        {
            _data = data;
        }

        public FByteBulkData(FAssetArchive Ar)
        {
            Header = new FByteBulkDataHeader(Ar);
            if (Header.ElementCount == 0 || BulkDataFlags.HasFlag(BULKDATA_Unused))
            {
                return;
            }

            _dataPosition = Ar.Position;
            _savedAr = Ar;

            if (BulkDataFlags.HasFlag(BULKDATA_ForceInlinePayload))
            {
                Ar.Position += Header.ElementCount;
            }
            else if (BulkDataFlags.HasFlag(BULKDATA_SerializeCompressedZLIB))
            {
                throw new ParserException(Ar, "错误:暂不支持ZLIB压缩数据");
            }

            if (LazyLoad)
            {
                _data = new Lazy<byte[]?>(() =>
                {
                    var data = new byte[Header.ElementCount];
                    return ReadBulkDataInto(data) ? data : null;
                });
            }
            else
            {
                var data = new byte[Header.ElementCount];
                if (ReadBulkDataInto(data)) _data = new Lazy<byte[]?>(() => data);
            }
        }

        protected FByteBulkData(FAssetArchive Ar, bool skip = false)
        {
            Header = new FByteBulkDataHeader(Ar);

            if (BulkDataFlags.HasFlag(BULKDATA_Unused | BULKDATA_PayloadInSeperateFile | BULKDATA_PayloadAtEndOfFile))
            {
                return;
            }

            if (BulkDataFlags.HasFlag(BULKDATA_ForceInlinePayload) || Header.OffsetInFile == Ar.Position)
            {
                Ar.Position += Header.SizeOnDisk;
            }
        }

        private void CheckReadSize(int read)
        {
            if (read != Header.ElementCount)
            {
                Log.Warning("读取了{read}字节，但预期是{Header.ElementCount}字节", read, Header.ElementCount);
            }
        }

        public bool ReadBulkDataInto(byte[] data, int offset = 0)
        {
            if (data.Length - offset < Header.ElementCount)
            {
                Log.Error("数据缓冲区太小");
                return false;
            }

            if (_savedAr == null)
            {
                Log.Error("存档读取器不可用");
                return false;
            }

            var Ar = (FAssetArchive)_savedAr.Clone();
            Ar.Position = _dataPosition;

            if (BulkDataFlags.HasFlag(BULKDATA_ForceInlinePayload))
            {
#if DEBUG
                Log.Debug("块数据在.uexp文件中(强制内联负载)(标志={BulkDataFlags},位置={HeaderOffsetInFile},大小={HeaderSizeOnDisk})", 
                    BulkDataFlags, Header.OffsetInFile, Header.SizeOnDisk);
#endif
                CheckReadSize(Ar.Read(data, offset, Header.ElementCount));
            }
            else if (BulkDataFlags.HasFlag(BULKDATA_OptionalPayload))
            {
#if DEBUG
                Log.Debug("块数据在{CookedIndex}.uptnl文件中(可选负载)(标志={BulkDataFlags},位置={HeaderOffsetInFile},大小={HeaderSizeOnDisk})", 
                    Header.CookedIndex, BulkDataFlags, Header.OffsetInFile, Header.SizeOnDisk);
#endif
                if (!TryGetBulkPayload(Ar, PayloadType.UPTNL, out var uptnlAr)) return false;

                CheckReadSize(uptnlAr.ReadAt(Header.OffsetInFile, data, offset, Header.ElementCount));
            }
            else if (BulkDataFlags.HasFlag(BULKDATA_PayloadInSeperateFile))
            {
#if DEBUG
                Log.Debug("块数据在{CookedIndex}.ubulk文件中(独立文件负载)(标志={BulkDataFlags},位置={HeaderOffsetInFile},大小={HeaderSizeOnDisk})", 
                    Header.CookedIndex, BulkDataFlags, Header.OffsetInFile, Header.SizeOnDisk);
#endif
                if (!TryGetBulkPayload(Ar, PayloadType.UBULK, out var ubulkAr)) return false;

                CheckReadSize(ubulkAr.ReadAt(Header.OffsetInFile, data, offset, Header.ElementCount));
            }
            else if (BulkDataFlags.HasFlag(BULKDATA_PayloadAtEndOfFile))
            {
#if DEBUG
                Log.Debug("块数据在.uexp文件中(文件末尾负载)(标志={BulkDataFlags},位置={HeaderOffsetInFile},大小={HeaderSizeOnDisk})", 
                    BulkDataFlags, Header.OffsetInFile, Header.SizeOnDisk);
#endif
                if (Header.OffsetInFile + Header.ElementCount <= Ar.Length)
                {
                    CheckReadSize(Ar.ReadAt(Header.OffsetInFile, data, offset, Header.ElementCount));
                }
                else throw new ParserException(Ar, $"读取文件末尾负载失败，位置{Header.OffsetInFile}超出范围");
            }
            else if (BulkDataFlags.HasFlag(BULKDATA_SerializeCompressedZLIB))
            {
                throw new ParserException(Ar, "错误:暂不支持ZLIB压缩数据");
            }
            else if (BulkDataFlags.HasFlag(BULKDATA_LazyLoadable) || BulkDataFlags.HasFlag(BULKDATA_None))
            {
                CheckReadSize(Ar.Read(data, offset, Header.ElementCount));
            }

            Ar.Dispose();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetBulkPayload(FAssetArchive Ar, PayloadType type, [MaybeNullWhen(false)] out FAssetArchive payloadAr)
        {
            payloadAr = null;
            if (Header.CookedIndex.IsDefault)
            {
                Ar.TryGetPayload(type, out payloadAr);
            }
            else if (Ar.Owner?.Provider is IVfsFileProvider vfsFileProvider)
            {
                var path = Path.ChangeExtension(Ar.Name, $"{Header.CookedIndex}.{type.ToString().ToLowerInvariant()}");
                if (vfsFileProvider.TryGetGameFile(path, out var file) && file.TryCreateReader(out var reader))
                {
                    payloadAr = new FAssetArchive(reader, Ar.Owner);
                }
            }
            return payloadAr != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDataSize() => Header.ElementCount;
    }
}