using System;
using CUE4Parse.UE4.VirtualFileSystem;
using AesProvider = CUE4Parse.Encryption.Aes.Aes;

namespace CUE4Parse.GameTypes.DreamStar.Encryption.Aes;

public static class DreamStarAes
{
    public static byte[] DreamStarDecrypt(byte[] bytes, int beginOffset, int count, bool isIndex, IAesVfsReader reader)
    {
        if (bytes.Length < beginOffset + count)
            throw new IndexOutOfRangeException("起始偏移量计数大于字节长度");
        if (count % 16 != 0)
            throw new ArgumentException("计数必须是16的倍数");
        if (reader.AesKey == null)
            throw new NullReferenceException("reader.AesKey");

        var output = AesProvider.Decrypt(bytes, beginOffset, count, reader.AesKey);

        for (var i = 0; i < count; i++)
        {
            output[i] = (byte) (((output[i] ^ 0x5F) << 5) | ((output[i] ^ 0x5F) >> 3));
        }

        return output;
    }
}