using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Pak.Objects;
using CUE4Parse.UE4.VirtualFileSystem;
using AesProvider = CUE4Parse.Encryption.Aes.Aes;

namespace CUE4Parse.GameTypes.DeltaForce.Encryption.Aes;

public static class DeltaForceAes
{
    public static byte[] DeltaForceDecrypt(byte[] bytes, int beginOffset, int count, bool isIndex, IAesVfsReader reader)
    {
        if (bytes.Length < beginOffset + count)
            throw new IndexOutOfRangeException("起始偏移量计数大于字节长度");
        if (count % 16 != 0)
            throw new ArgumentException("计数必须是16的倍数");
        if (reader.AesKey == null)
            throw new NullReferenceException("reader.AesKey");

        var output = AesProvider.Decrypt(bytes, beginOffset, count, reader.AesKey);

        var pakInfo = (reader as PakFileReader)?.Info;
        if (pakInfo is null) throw new NullReferenceException("reader.Info");

        if (pakInfo.CustomEncryptionData is null)
        {
            if (!isIndex) throw new ParserException($"无法解密没有xor值的pak{reader.Name}");

            var field = typeof(FPakInfo).GetField("CustomEncryptionData", BindingFlags.Instance | BindingFlags.Public);

            var xorValue = output[0];
            if (field is not null && output.Take(4).All(x => x == xorValue))
            {
                field.SetValueDirect(__makeref(pakInfo), Enumerable.Repeat(xorValue, 8).ToArray());
            }
            else
            {
                throw new ParserException($"找不到正确的xor值来解密pak{reader.Name}");
            }
        }

        var xorKey = BitConverter.ToUInt64(pakInfo.CustomEncryptionData.AsSpan(..8));
        var span = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, ulong>(ref output[0]), count >> 3);
        for (var i = 0; i < span.Length; i++)
        {
            span[i] ^= xorKey;
        }

        return output;
    }
}