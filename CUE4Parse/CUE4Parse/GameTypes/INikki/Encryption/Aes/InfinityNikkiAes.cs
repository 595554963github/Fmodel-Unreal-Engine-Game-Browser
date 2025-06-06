using System;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.VirtualFileSystem;
using AesProvider = CUE4Parse.Encryption.Aes.Aes;

namespace CUE4Parse.GameTypes.INikki.Encryption.Aes;

public static class InfinityNikkiAes
{
    public static byte[] InfinityNikkiDecrypt(byte[] bytes, int beginOffset, int count, bool isIndex, IAesVfsReader reader)
    {
        if (bytes.Length < beginOffset + count)
            throw new IndexOutOfRangeException("起始偏移量计数大于字节长度");
        if (count % 16 != 0)
            throw new ArgumentException("计数必须是16的倍数");
        if (reader.AesKey == null)
            throw new NullReferenceException("reader.AesKey");

        var output = AesProvider.Decrypt(bytes, beginOffset, count, reader.AesKey);

        if (reader is PakFileReader || isIndex)
        {
            var key = reader.AesKey.Key;
            for (var i = 0; i < count >> 4; i++)
            {
                output[i * 16] ^= key[0];
                output[i * 16 + 15] ^= key[31];
            }
        }

        return output;
    }
}