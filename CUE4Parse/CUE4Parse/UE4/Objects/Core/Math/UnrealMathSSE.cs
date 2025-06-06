using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CUE4Parse.UE4.Objects.Core.Math
{
    public static class UnrealMathSSE
    {
        public static readonly Vector128<float> QMULTI_SIGN_MASK0 = Vector128.Create(1f, -1f, 1f, -1f);
        public static readonly Vector128<float> QMULTI_SIGN_MASK1 = Vector128.Create(1f, 1f, -1f, -1f);
        public static readonly Vector128<float> QMULTI_SIGN_MASK2 = Vector128.Create(-1f, 1f, 1f, -1f);

        // Precomputed shuffle masks for common operations
        private const byte REPLICATE_X_MASK = 0b00000000; // 0,0,0,0
        private const byte REPLICATE_Y_MASK = 0b01010101; // 1,1,1,1
        private const byte REPLICATE_Z_MASK = 0b10101010; // 2,2,2,2
        private const byte REPLICATE_W_MASK = 0b11111111; // 3,3,3,3
        private const byte SWIZZLE_3_2_1_0 = 0b00011011; // 3,2,1,0
        private const byte SWIZZLE_2_3_0_1 = 0b10001110; // 2,3,0,1
        private const byte SWIZZLE_1_0_3_2 = 0b01101001; // 1,0,3,2

        public static byte ShuffleMask(byte A0, byte A1, byte B2, byte B3)
        {
            return (byte)(A0 | (A1 << 2) | (B2 << 4) | (B3 << 6));
        }

        public static int RoundToInt(float val)
        {
            if (!Sse.IsSupported)
            {
                return (int)MathF.Floor((val * 2.0f) + 0.5f) >> 1;
            }

            Vector128<float> vec = Vector128.Create(val * 2.0f + 0.5f);
            return Sse.ConvertToInt32(vec) >> 1;
        }

        public static Vector128<float> VectorReplicate(Vector128<float> vec, byte elementIndex)
        {
            return elementIndex switch
            {
                0 => Sse.Shuffle(vec, vec, REPLICATE_X_MASK),
                1 => Sse.Shuffle(vec, vec, REPLICATE_Y_MASK),
                2 => Sse.Shuffle(vec, vec, REPLICATE_Z_MASK),
                3 => Sse.Shuffle(vec, vec, REPLICATE_W_MASK),
                _ => throw new ArgumentOutOfRangeException(nameof(elementIndex), "元素索引必须在0到3之间")
            };
        }

        public static Vector128<float> VectorMultiply(Vector128<float> vec1, Vector128<float> vec2)
        {
            return Sse.Multiply(vec1, vec2);
        }

        public static Vector128<float> VectorSwizzle(Vector128<float> vec, byte x, byte y, byte z, byte w)
        {
            // For the specific swizzle patterns used in quaternion multiplication,
            // we use precomputed constants for better performance
            if (x == 3 && y == 2 && z == 1 && w == 0)
                return Sse.Shuffle(vec, vec, SWIZZLE_3_2_1_0);
            if (x == 2 && y == 3 && z == 0 && w == 1)
                return Sse.Shuffle(vec, vec, SWIZZLE_2_3_0_1);
            if (x == 1 && y == 0 && z == 3 && w == 2)
                return Sse.Shuffle(vec, vec, SWIZZLE_1_0_3_2);
            // Fallback for general case
            const byte fallbackMask = 0b00011011; 
            return Sse.Shuffle(vec, vec, fallbackMask);
        }

        public static Vector128<float> VectorMultiplyAdd(Vector128<float> vec1, Vector128<float> vec2, Vector128<float> vec3)
        {
            return Sse.Add(Sse.Multiply(vec1, vec2), vec3);
        }

        public static FQuat VectorQuaternionMultiply2(FQuat quat1, FQuat quat2)
        {
            var vec1 = FQuat.AsVector128(quat1);
            var vec2 = FQuat.AsVector128(quat2);

            var r = VectorMultiply(VectorReplicate(vec1, 3), vec2);
            r = VectorMultiplyAdd(VectorMultiply(VectorReplicate(vec1, 0), VectorSwizzle(vec2, 3, 2, 1, 0)), QMULTI_SIGN_MASK0, r);
            r = VectorMultiplyAdd(VectorMultiply(VectorReplicate(vec1, 1), VectorSwizzle(vec2, 2, 3, 0, 1)), QMULTI_SIGN_MASK1, r);
            r = VectorMultiplyAdd(VectorMultiply(VectorReplicate(vec1, 2), VectorSwizzle(vec2, 1, 0, 3, 2)), QMULTI_SIGN_MASK2, r);

            float x = r.GetElement(0);
            float y = r.GetElement(1);
            float z = r.GetElement(2);
            float w = r.GetElement(3);

            return new FQuat(x, y, z, w);
        }
    }
}