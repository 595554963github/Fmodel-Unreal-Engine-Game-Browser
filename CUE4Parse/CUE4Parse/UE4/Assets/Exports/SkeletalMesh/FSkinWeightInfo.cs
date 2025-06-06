using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.SkeletalMesh
{
    public class FSkinWeightInfo
    {
        private const int NUM_INFLUENCES_UE4 = 4;
        private const int MAX_TOTAL_INFLUENCES_UE4 = 8;

        public ushort[] BoneIndex { get; }
        public byte[] BoneWeight { get; }
        public bool Use16BitBoneWeight { get; } = false;

        public FSkinWeightInfo()
        {
            BoneIndex = new ushort[NUM_INFLUENCES_UE4];
            BoneWeight = new byte[NUM_INFLUENCES_UE4];
        }

        public FSkinWeightInfo(FArchive Ar, bool bExtraBoneInfluences, bool bUse16BitBoneIndex = false, int length = 0)
            : this()
        {
            int numSkelInfluences = CalculateInfluenceCount(bExtraBoneInfluences, length);

            BoneIndex = ReadBoneIndices(Ar, numSkelInfluences, bUse16BitBoneIndex);
            BoneWeight = Ar.ReadArray<byte>(numSkelInfluences);
        }

        private static int CalculateInfluenceCount(bool bExtraBoneInfluences, int length)
        {
            if (length > 0)
                return length;

            return bExtraBoneInfluences ? MAX_TOTAL_INFLUENCES_UE4 : NUM_INFLUENCES_UE4;
        }

        private static ushort[] ReadBoneIndices(FArchive Ar, int count, bool bUse16BitBoneIndex)
        {
            return bUse16BitBoneIndex
                ? Ar.ReadArray<ushort>(count)
                : Ar.ReadArray(count, () => (ushort)Ar.Read<byte>());
        }
    }
}