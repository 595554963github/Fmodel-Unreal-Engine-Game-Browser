using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;

namespace CUE4Parse.UE4.Pak.Objects;

public partial class FPakInfo
{
    private static unsafe void DecryptInZOIFPakInfo(FArchive Ar, long maxOffset, byte* buffer)
    {
        int* lastInt = (int*)(buffer + maxOffset - 4);
        if (*lastInt == 0) return;

        var directoryName = Path.GetDirectoryName(Ar.Name);
        if (string.IsNullOrEmpty(directoryName))
            throw new ParserException("�޷�����pak�ļ�,�޷�ȷ��public_key.pem��·��");

        var path = Path.Combine(directoryName, "public_key.pem");
        if (!File.Exists(path))
            throw new ParserException("�޷�����pak�ļ�,public_key.pemδ�ҵ�");
        AsymmetricKeyParameter publicKey = ReadPublicKeyFromPem(path);
        static AsymmetricKeyParameter ReadPublicKeyFromPem(string publicKeyPemPath)
        {
            using var reader = new StreamReader(publicKeyPemPath);
            var pemReader = new PemReader(reader);
            return (AsymmetricKeyParameter)pemReader.ReadObject();
        }

        var rsaEngine = new Pkcs1Encoding(new RsaEngine());
        rsaEngine.Init(false, publicKey);

        Ar.Seek(-263, SeekOrigin.End);
        var part = Ar.ReadBytes(7);
        var data = Ar.ReadBytes(256);
        // Decrypt the data
        var decryptedData = rsaEngine.ProcessBlock(data, 0, data.Length);
        var combined = part.Concat(decryptedData).ToArray();
        var pos = (int)maxOffset - combined.Length;
        Unsafe.CopyBlockUnaligned(ref buffer[pos], ref combined[0], (uint)combined.Length);
    }
}
