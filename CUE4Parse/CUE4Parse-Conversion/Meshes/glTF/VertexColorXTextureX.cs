﻿using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace CUE4Parse_Conversion.Meshes.glTF
{
    public struct VertexColorXTextureX : IVertexMaterial, IVertexReflection, IEquatable<VertexColorXTextureX>
    {
        public int MaxColors => 1;
        public int MaxTextCoords => 8;

        public Vector4 Color;
        public Vector2 TexCoord0;
        public Vector2 TexCoord1;
        public Vector2 TexCoord2;
        public Vector2 TexCoord3;
        public Vector2 TexCoord4;
        public Vector2 TexCoord5;
        public Vector2 TexCoord6;
        public Vector2 TexCoord7;

        public VertexColorXTextureX(Vector4 color, List<Vector2> texCoords)
        {
            Color = color;

            texCoords.Capacity = Math.Max(texCoords.Count, 8);
            Resize(texCoords, texCoords.Capacity, new Vector2(0, 0));
            TexCoord0 = texCoords[0];
            TexCoord1 = texCoords[1];
            TexCoord2 = texCoords[2];
            TexCoord3 = texCoords[3];
            TexCoord4 = texCoords[4];
            TexCoord5 = texCoords[5];
            TexCoord6 = texCoords[6];
            TexCoord7 = texCoords[7];
        }

        public void SetColor(int setIndex, Vector4 color)
        {
            if (setIndex != 0) throw new ArgumentOutOfRangeException(nameof(setIndex));
            Color = color;
        }

        public void SetTexCoord(int setIndex, Vector2 coord)
        {
            switch (setIndex)
            {
                case 0: TexCoord0 = coord; break;
                case 1: TexCoord1 = coord; break;
                case 2: TexCoord2 = coord; break;
                case 3: TexCoord3 = coord; break;
                case 4: TexCoord4 = coord; break;
                case 5: TexCoord5 = coord; break;
                case 6: TexCoord6 = coord; break;
                case 7: TexCoord7 = coord; break;
                default: throw new ArgumentOutOfRangeException(nameof(setIndex));
            }
        }

        public Vector2 GetTexCoord(int index)
        {
            switch (index)
            {
                case 0: return TexCoord0;
                case 1: return TexCoord1;
                case 2: return TexCoord2;
                case 3: return TexCoord3;
                case 4: return TexCoord4;
                case 5: return TexCoord5;
                case 6: return TexCoord6;
                case 7: return TexCoord7;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public Vector4 GetColor(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return Color;
        }

        VertexMaterialDelta IVertexMaterial.Subtract(IVertexMaterial baseValue)
        {
            if (baseValue is not VertexColorXTextureX other)
                throw new ArgumentException("Incompatible vertex types");

            var delta = new VertexMaterialDelta
            {
                Color0Delta = Color - other.Color
            };

            for (int i = 0; i < MaxTextCoords; i++)
            {
                switch (i)
                {
                    case 0: delta.TexCoord0Delta = GetTexCoord(i) - other.GetTexCoord(i); break;
                    case 1: delta.TexCoord1Delta = GetTexCoord(i) - other.GetTexCoord(i); break;
                    case 2: delta.TexCoord2Delta = GetTexCoord(i) - other.GetTexCoord(i); break;
                    case 3: delta.TexCoord3Delta = GetTexCoord(i) - other.GetTexCoord(i); break;
                    default: throw new ArgumentOutOfRangeException(nameof(i), "Invalid texture coordinate index");
                }
            }

            return delta;
        }

        public void Add(in VertexMaterialDelta delta)
        {
            Color += delta.GetColor(0);

            for (int i = 0; i < MaxTextCoords; i++)
            {
                if (i < delta.MaxTextCoords)
                {
                    switch (i)
                    {
                        case 0: TexCoord0 += delta.GetTexCoord(i); break;
                        case 1: TexCoord1 += delta.GetTexCoord(i); break;
                        case 2: TexCoord2 += delta.GetTexCoord(i); break;
                        case 3: TexCoord3 += delta.GetTexCoord(i); break;
                        case 4: TexCoord4 += delta.GetTexCoord(i); break;
                        case 5: TexCoord5 += delta.GetTexCoord(i); break;
                        case 6: TexCoord6 += delta.GetTexCoord(i); break;
                        case 7: TexCoord7 += delta.GetTexCoord(i); break;
                    }
                }
            }
        }

        IEnumerable<KeyValuePair<string, AttributeFormat>> IVertexReflection.GetEncodingAttributes()
        {
            yield return new KeyValuePair<string, AttributeFormat>("COLOR_0", new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));

            for (int i = 0; i < MaxTextCoords; i++)
            {
                yield return new KeyValuePair<string, AttributeFormat>($"TEXCOORD_{i}", new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetVertexSizeInBytes()
        {
            return Unsafe.SizeOf<VertexColorXTextureX>();
        }

        private static void Resize<T>(List<T> list, int size, T val)
        {
            if (size > list.Count)
                while (size - list.Count > 0)
                    list.Add(val);
            else if (size < list.Count)
                while (list.Count - size > 0)
                    list.RemoveAt(list.Count - 1);
        }

        public bool Equals(VertexColorXTextureX other)
        {
            return other.Color == Color &&
                   other.TexCoord0 == TexCoord0 &&
                   other.TexCoord1 == TexCoord1 &&
                   other.TexCoord2 == TexCoord2 &&
                   other.TexCoord3 == TexCoord3 &&
                   other.TexCoord4 == TexCoord4 &&
                   other.TexCoord5 == TexCoord5 &&
                   other.TexCoord6 == TexCoord6 &&
                   other.TexCoord7 == TexCoord7;
        }
    }
}