using System;

using CUE4Parse.ACL;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Animation.ACL
{
    [JsonConverter(typeof(FACLCompressedAnimDataConverter))]
    public class FACLCompressedAnimData : ICompressedAnimData
    {
        public int CompressedNumberOfFrames { get; set; }

        public byte[] CompressedByteStream { get; set; } = Array.Empty<byte>();

        public CompressedTracks GetCompressedTracks() => new(CompressedByteStream);

        public void Bind(byte[] bulkData) => CompressedByteStream = bulkData;
    }

    public abstract class UAnimBoneCompressionCodec_ACLBase : UAnimBoneCompressionCodec
    {
        public override ICompressedAnimData AllocateAnimData() => new FACLCompressedAnimData();
    }
}