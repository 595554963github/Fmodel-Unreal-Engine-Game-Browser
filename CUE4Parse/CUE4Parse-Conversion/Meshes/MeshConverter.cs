using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse_Conversion.Landscape;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.RenderCore;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;

namespace CUE4Parse_Conversion.Meshes;

public static class MeshConverter
{
    private static class Constants
    {
        public const float UShort_Bone_Scale = 1.0f / 65535.0f;
        public const float Byte_Bone_Scale = 1.0f / 255.0f;
        public const int MAX_MESH_UV_SETS = 4;
    }

    public static bool TryConvert(this USkeleton originalSkeleton, out List<CSkelMeshBone> bones, out FBox box)
    {
        bones = new List<CSkelMeshBone>();
        box = new FBox();
        if (originalSkeleton?.ReferenceSkeleton?.FinalRefBoneInfo == null ||
            originalSkeleton.ReferenceSkeleton.FinalRefBonePose == null)
            return false;

        for (var i = 0; i < originalSkeleton.ReferenceSkeleton.FinalRefBoneInfo.Length; i++)
        {
            var skeletalMeshBone = new CSkelMeshBone
            {
                Name = originalSkeleton.ReferenceSkeleton.FinalRefBoneInfo[i].Name,
                ParentIndex = originalSkeleton.ReferenceSkeleton.FinalRefBoneInfo[i].ParentIndex,
                Position = originalSkeleton.ReferenceSkeleton.FinalRefBonePose[i].Translation,
                Orientation = originalSkeleton.ReferenceSkeleton.FinalRefBonePose[i].Rotation,
            };

            bones.Add(skeletalMeshBone);
            box.Min = skeletalMeshBone.Position.ComponentMin(box.Min);
            box.Max = skeletalMeshBone.Position.ComponentMax(box.Max);
        }
        return true;
    }

    public static bool TryConvert(this USplineMeshComponent? spline, out CStaticMesh convertedMesh)
    {
        var originalMesh = spline?.GetStaticMesh()?.Load<UStaticMesh>();
        if (originalMesh == null)
        {
            convertedMesh = new CStaticMesh();
            return false;
        }
        return TryConvert(originalMesh, spline, out convertedMesh);
    }

    public static bool TryConvert(this UStaticMesh originalMesh, out CStaticMesh convertedMesh)
    {
        return TryConvert(originalMesh, null, out convertedMesh);
    }

    public static bool TryConvert(this UStaticMesh originalMesh, USplineMeshComponent? spline, out CStaticMesh convertedMesh)
    {
        convertedMesh = new CStaticMesh();
        if (originalMesh?.RenderData == null)
            return false;

        convertedMesh.BoundingSphere = new FSphere(0f, 0f, 0f, originalMesh.RenderData.Bounds?.SphereRadius / 2 ?? 0);
        convertedMesh.BoundingBox = originalMesh.RenderData.Bounds != null
            ? new FBox(
                originalMesh.RenderData.Bounds.Origin - originalMesh.RenderData.Bounds.BoxExtent,
                originalMesh.RenderData.Bounds.Origin + originalMesh.RenderData.Bounds.BoxExtent)
            : new FBox();

        if (originalMesh.RenderData.LODs == null)
            return false;

        foreach (var srcLod in originalMesh.RenderData.LODs)
        {
            if (srcLod?.SkipLod == true) continue;

            var numTexCoords = srcLod!.VertexBuffer?.NumTexCoords ?? 0;
            var numVerts = srcLod.PositionVertexBuffer?.Verts?.Length ?? 0;
            if (numVerts == 0 && numTexCoords == 0)
            {
                continue;
            }

            if (numTexCoords > Constants.MAX_MESH_UV_SETS)
                throw new ParserException($"静态网格包含过多的UV集({numTexCoords})，最大支持{Constants.MAX_MESH_UV_SETS}个");

            var staticMeshLod = new CStaticMeshLod
            {
                NumTexCoords = numTexCoords,
                HasNormals = true,
                HasTangents = true,
                IsTwoSided = srcLod.CardRepresentationData?.bMostlyTwoSided ?? false,
                Indices = new Lazy<FRawStaticIndexBuffer>(srcLod.IndexBuffer ?? new FRawStaticIndexBuffer()),
                Sections = new Lazy<CMeshSection[]>(() =>
                {
                    if (srcLod.Sections == null) return Array.Empty<CMeshSection>();

                    var sections = new CMeshSection[srcLod.Sections.Length];
                    for (var j = 0; j < sections.Length; j++)
                    {
                        int materialIndex = srcLod.Sections[j].MaterialIndex;
                        while (materialIndex >= (originalMesh.Materials?.Length ?? 0))
                        {
                            materialIndex--;
                        }

                        if (materialIndex < 0)
                        {
                            sections[j] = new CMeshSection(srcLod.Sections[j]);
                        }
                        else
                        {
                            sections[j] = new CMeshSection(
                                materialIndex,
                                srcLod.Sections[j],
                                originalMesh.StaticMaterials?[materialIndex].MaterialSlotName.Text,
                                originalMesh.Materials?[materialIndex]);
                        }
                    }
                    return sections;
                })
            };

            staticMeshLod.AllocateVerts(numVerts);
            if (srcLod.ColorVertexBuffer != null && srcLod.ColorVertexBuffer.NumVertices != 0)
                staticMeshLod.AllocateVertexColorBuffer();

            for (var j = 0; j < numVerts; j++)
            {
                if (srcLod.VertexBuffer == null || srcLod.VertexBuffer.UV == null || j >= srcLod.VertexBuffer.UV.Length)
                    continue;

                var suv = srcLod.VertexBuffer.UV[j];
                if (suv.Normal[1].Data != 0)
                    throw new NotImplementedException("不支持的功能: 此特性仅适用于UE3版本");

                if (srcLod.PositionVertexBuffer == null || j >= srcLod.PositionVertexBuffer.Verts.Length)
                    continue;

                var pos = srcLod.PositionVertexBuffer.Verts[j];
                if (spline != null)
                {
                    var distanceAlong = USplineMeshComponent.GetAxisValueRef(ref pos, spline.ForwardAxis);
                    var sliceTransform = spline.CalcSliceTransform(distanceAlong);
                    USplineMeshComponent.SetAxisValueRef(ref pos, spline.ForwardAxis, 0f);
                    pos = sliceTransform.TransformPosition(pos);
                }

                staticMeshLod.Verts[j].Position = pos;
                UnpackNormals(suv.Normal, staticMeshLod.Verts[j]);
                staticMeshLod.Verts[j].UV.U = suv.UV[0].U;
                staticMeshLod.Verts[j].UV.V = suv.UV[0].V;

                if (staticMeshLod.ExtraUV?.Value != null)
                {
                    for (var k = 1; k < numTexCoords; k++)
                    {
                        if (k - 1 < staticMeshLod.ExtraUV.Value.Length && k < suv.UV.Length)
                        {
                            staticMeshLod.ExtraUV.Value[k - 1][j].U = suv.UV[k].U;
                            staticMeshLod.ExtraUV.Value[k - 1][j].V = suv.UV[k].V;
                        }
                    }
                }

                if (srcLod.ColorVertexBuffer != null &&
                    srcLod.ColorVertexBuffer.NumVertices != 0 &&
                    j < srcLod.ColorVertexBuffer.Data.Length &&
                    staticMeshLod.VertexColors != null)
                {
                    staticMeshLod.VertexColors[j] = srcLod.ColorVertexBuffer.Data[j];
                }
            }

            convertedMesh.LODs.Add(staticMeshLod);
        }

        convertedMesh.FinalizeMesh();
        return true;
    }

    public static bool TryConvert(this USkeletalMesh originalMesh, out CSkeletalMesh convertedMesh)
    {
        convertedMesh = new CSkeletalMesh();
        if (originalMesh?.LODModels == null)
            return false;

        convertedMesh.BoundingSphere = new FSphere(0f, 0f, 0f, originalMesh.ImportedBounds?.SphereRadius / 2 ?? 0);
        convertedMesh.BoundingBox = originalMesh.ImportedBounds != null
            ? new FBox(
                originalMesh.ImportedBounds.Origin - originalMesh.ImportedBounds.BoxExtent,
                originalMesh.ImportedBounds.Origin + originalMesh.ImportedBounds.BoxExtent)
            : new FBox();

        foreach (var srcLod in originalMesh.LODModels)
        {
            if (srcLod?.SkipLod == true) continue;

            var numTexCoords = srcLod!.NumTexCoords;
            if (numTexCoords > Constants.MAX_MESH_UV_SETS)
                throw new ParserException($"骨骼网格包含过多的UV集({numTexCoords})，最大支持{Constants.MAX_MESH_UV_SETS}个");

            var skeletalMeshLod = new CSkelMeshLod
            {
                NumTexCoords = numTexCoords,
                HasNormals = true,
                HasTangents = true,
                Indices = new Lazy<FRawStaticIndexBuffer>(() => new FRawStaticIndexBuffer
                {
                    Indices16 = srcLod.Indices?.Indices16 ?? Array.Empty<ushort>(),
                    Indices32 = srcLod.Indices?.Indices32 ?? Array.Empty<uint>()
                }),
                Sections = new Lazy<CMeshSection[]>(() =>
                {
                    if (srcLod.Sections == null) return Array.Empty<CMeshSection>();

                    var sections = new CMeshSection[srcLod.Sections.Length];
                    for (var j = 0; j < sections.Length; j++)
                    {
                        int materialIndex = srcLod.Sections[j].MaterialIndex;
                        if (materialIndex < 0)
                        {
                            materialIndex = 0;
                        }
                        else while (materialIndex >= (originalMesh.Materials?.Length ?? 0))
                            {
                                materialIndex--;
                            }

                        if (materialIndex < 0)
                        {
                            sections[j] = new CMeshSection(srcLod.Sections[j]);
                        }
                        else
                        {
                            sections[j] = new CMeshSection(
                                materialIndex,
                                srcLod.Sections[j],
                                originalMesh.SkeletalMaterials?[materialIndex].MaterialSlotName.Text,
                                originalMesh.SkeletalMaterials?[materialIndex].Material);
                        }
                    }
                    return sections;
                })
            };

            var bUseVerticesFromSections = false;
            var vertexCount = srcLod.VertexBufferGPUSkin?.GetVertexCount() ?? 0;
            if (vertexCount == 0 && srcLod.Sections?.Length > 0 && srcLod.Sections[0].SoftVertices?.Length > 0)
            {
                bUseVerticesFromSections = true;
                foreach (var section in srcLod.Sections)
                {
                    vertexCount += section.SoftVertices?.Length ?? 0;
                }
            }

            skeletalMeshLod.AllocateVerts(vertexCount);

            var chunkIndex = -1;
            var chunkVertexIndex = 0;
            long lastChunkVertex = -1;
            ushort[]? boneMap = null;
            var vertBuffer = srcLod.VertexBufferGPUSkin;

            if (srcLod.ColorVertexBuffer?.Data?.Length == vertexCount)
                skeletalMeshLod.AllocateVertexColorBuffer();

            for (var vert = 0; vert < vertexCount; vert++)
            {
                while (vert >= lastChunkVertex)
                {
                    if (srcLod.Chunks?.Length > 0)
                    {
                        var c = srcLod.Chunks[++chunkIndex];
                        lastChunkVertex = c.BaseVertexIndex + c.NumRigidVertices + c.NumSoftVertices;
                        boneMap = c.BoneMap;
                    }
                    else if (srcLod.Sections?.Length > 0)
                    {
                        var s = srcLod.Sections[++chunkIndex];
                        lastChunkVertex = s.BaseVertexIndex + s.NumVertices;
                        boneMap = s.BoneMap;
                    }
                    else
                    {
                        break;
                    }

                    chunkVertexIndex = 0;
                }

                FSkelMeshVertexBase? v = null;
                if (bUseVerticesFromSections &&
                    srcLod.Sections != null &&
                    chunkIndex < srcLod.Sections.Length &&
                    srcLod.Sections[chunkIndex].SoftVertices != null &&
                    chunkVertexIndex < srcLod.Sections[chunkIndex].SoftVertices.Length)
                {
                    var v0 = srcLod.Sections[chunkIndex].SoftVertices[chunkVertexIndex++];
                    v = v0;

                    skeletalMeshLod.Verts[vert].UV = v0.UV[0];
                    if (skeletalMeshLod.ExtraUV?.Value != null)
                    {
                        for (var texCoordIndex = 1; texCoordIndex < numTexCoords; texCoordIndex++)
                        {
                            if (texCoordIndex - 1 < skeletalMeshLod.ExtraUV.Value.Length &&
                                texCoordIndex < v0.UV.Length)
                            {
                                skeletalMeshLod.ExtraUV.Value[texCoordIndex - 1][vert] = v0.UV[texCoordIndex];
                            }
                        }
                    }
                }
                else if (vertBuffer != null && !vertBuffer.bUseFullPrecisionUVs &&
                         vertBuffer.VertsHalf != null && vert < vertBuffer.VertsHalf.Length)
                {
                    var v0 = vertBuffer.VertsHalf[vert];
                    v = v0;

                    skeletalMeshLod.Verts[vert].UV = (FMeshUVFloat)v0.UV[0];
                    if (skeletalMeshLod.ExtraUV?.Value != null)
                    {
                        for (var texCoordIndex = 1; texCoordIndex < numTexCoords; texCoordIndex++)
                        {
                            if (texCoordIndex - 1 < skeletalMeshLod.ExtraUV.Value.Length &&
                                texCoordIndex < v0.UV.Length)
                            {
                                skeletalMeshLod.ExtraUV.Value[texCoordIndex - 1][vert] = (FMeshUVFloat)v0.UV[texCoordIndex];
                            }
                        }
                    }
                }
                else if (vertBuffer != null && vertBuffer.VertsFloat != null && vert < vertBuffer.VertsFloat.Length)
                {
                    var v0 = vertBuffer.VertsFloat[vert];
                    v = v0;

                    skeletalMeshLod.Verts[vert].UV = v0.UV[0];
                    if (skeletalMeshLod.ExtraUV?.Value != null)
                    {
                        for (var texCoordIndex = 1; texCoordIndex < numTexCoords; texCoordIndex++)
                        {
                            if (texCoordIndex - 1 < skeletalMeshLod.ExtraUV.Value.Length &&
                                texCoordIndex < v0.UV.Length)
                            {
                                skeletalMeshLod.ExtraUV.Value[texCoordIndex - 1][vert] = v0.UV[texCoordIndex];
                            }
                        }
                    }
                }

                if (v == null) continue;

                skeletalMeshLod.Verts[vert].Position = v.Pos;
                UnpackNormals(v.Normal, skeletalMeshLod.Verts[vert]);
                if (skeletalMeshLod.VertexColors != null &&
                    srcLod.ColorVertexBuffer?.Data != null &&
                    vert < srcLod.ColorVertexBuffer.Data.Length)
                {
                    skeletalMeshLod.VertexColors[vert] = srcLod.ColorVertexBuffer.Data[vert];
                }

                if (v.Infs != null)
                {
                    bool bUse16BitBoneWeight = v.Infs.GetType().GetProperty("bUse16BitBoneWeight")?.GetValue(v.Infs) as bool? ?? false;
                    var scale = bUse16BitBoneWeight ? Constants.UShort_Bone_Scale : Constants.Byte_Bone_Scale;

                    if (v.Infs.BoneWeight != null && v.Infs.BoneIndex != null)
                    {
                        foreach (var (weight, boneIndex) in v.Infs.BoneWeight.Zip(v.Infs.BoneIndex))
                        {
                            if (weight != 0f && boneMap != null && boneIndex < boneMap.Length)
                            {
                                var bone = boneMap[boneIndex];
                                skeletalMeshLod.Verts[vert].AddInfluence(bone, weight, weight * scale);
                            }
                        }
                    }
                }
            }

            convertedMesh.LODs.Add(skeletalMeshLod);
        }

        if (originalMesh.ReferenceSkeleton?.FinalRefBoneInfo != null &&
            originalMesh.ReferenceSkeleton.FinalRefBonePose != null)
        {
            for (var i = 0; i < originalMesh.ReferenceSkeleton.FinalRefBoneInfo.Length; i++)
            {
                var skeletalMeshBone = new CSkelMeshBone
                {
                    Name = originalMesh.ReferenceSkeleton.FinalRefBoneInfo[i].Name,
                    ParentIndex = originalMesh.ReferenceSkeleton.FinalRefBoneInfo[i].ParentIndex,
                    Position = originalMesh.ReferenceSkeleton.FinalRefBonePose[i].Translation,
                    Orientation = originalMesh.ReferenceSkeleton.FinalRefBonePose[i].Rotation
                };

                convertedMesh.RefSkeleton.Add(skeletalMeshBone);
            }
        }

        convertedMesh.FinalizeMesh();
        return true;
    }

    public struct FSectionRange
    {
        public int End;
        public int Index;
    }

    private static void UnpackNormals(FPackedNormal[] normal, CMeshVertex v)
    {
        if (normal == null || normal.Length < 3) return;

        v.Tangent = normal[0];
        v.Normal = normal[2];

        if (normal[1].Data != 0)
        {
            throw new NotImplementedException("不支持的功能: 此特性仅适用于UE3版本");
        }
    }

    public static bool TryConvert(this ALandscapeProxy landscape, ULandscapeComponent[]? landscapeComponents, ELandscapeExportFlags flags, out CStaticMesh? convertedMesh, out Dictionary<string, Image> heightMaps, out Dictionary<string, SKBitmap> weightMaps)
    {
        heightMaps = new Dictionary<string, Image>();
        weightMaps = new Dictionary<string, SKBitmap>();
        convertedMesh = null;

        if (landscapeComponents == null)
        {
            var comps = landscape.LandscapeComponents;
            if (comps == null) return false;

            landscapeComponents = new ULandscapeComponent[comps.Length];
            for (var i = 0; i < comps.Length; i++)
                landscapeComponents[i] = comps[i]?.Load<ULandscapeComponent>() ?? new ULandscapeComponent();
        }

        var componentSize = landscape.ComponentSizeQuads;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var comp in landscapeComponents)
        {
            if (comp == null) continue;

            if (componentSize == -1)
                componentSize = comp.ComponentSizeQuads;
            else
            {
                Debug.Assert(componentSize == comp.ComponentSizeQuads);
            }

            comp.GetComponentExtent(ref minX, ref minY, ref maxX, ref maxY);
        }

        int componentSizeQuads = ((componentSize + 1) >> 0) - 1;
        float scaleFactor = (float)componentSizeQuads / componentSize;
        int numComponents = landscapeComponents.Length;
        int vertexCountPerComponent = (componentSizeQuads + 1) * (componentSizeQuads + 1);
        int vertexCount = numComponents * vertexCountPerComponent;
        int triangleCount = numComponents * (componentSizeQuads * componentSizeQuads) * 2;

        FVector2D uvScale = new FVector2D(1.0f, 1.0f) / new FVector2D((maxX - minX) + 1, (maxY - minY) + 1);

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        CStaticMeshLod? landscapeLod = null;
        if (flags.HasFlag(ELandscapeExportFlags.Mesh))
        {
            landscapeLod = new CStaticMeshLod();
            landscapeLod.NumTexCoords = 2;
            landscapeLod.AllocateVerts(vertexCount);
            landscapeLod.AllocateVertexColorBuffer();
        }

        var extraVertexColorMap = new ConcurrentDictionary<string, CVertexColor>();
        var weightMapsInternal = new Dictionary<string, SKBitmap>();
        var weightMapsPixels = new Dictionary<int, IntPtr>();
        var weightMapLock = new object();
        var heightMapData = new L16[height * width];

        var tasks = new Task[landscapeComponents.Length];
        for (int i = 0, selectedComponentIndex = 0; i < landscapeComponents.Length; i++)
        {
            var comp = landscapeComponents[i];
            if (comp == null) continue;

            var CDI = new FLandscapeComponentDataInterface(comp, 0);
            CDI.EnsureWeightmapTextureDataCache();

            int baseVertIndex = selectedComponentIndex++ * vertexCountPerComponent;

            var weightMapAllocs = comp.GetWeightmapLayerAllocations();
            var compTransform = comp.GetComponentTransform();
            var relLoc = comp.GetRelativeLocation();

            tasks[i] = Task.Run(() =>
            {
                for (int vertIndex = 0; vertIndex < vertexCountPerComponent; vertIndex++)
                {
                    CDI.VertexIndexToXY(vertIndex, out var vertX, out var vertY);

                    var vertCoord = CDI.GetLocalVertex(vertX, vertY);
                    var position = vertCoord + relLoc;

                    CDI.GetLocalTangentVectors(vertIndex, out var tangentX, out var biNormal, out var normal);

                    normal /= compTransform.Scale3D;
                    normal.Normalize();
                    FVector4.AsFVector(ref tangentX) /= compTransform.Scale3D;
                    FVector4.AsFVector(ref tangentX).Normalize();
                    biNormal /= compTransform.Scale3D;
                    biNormal.Normalize();

                    var textureUv = new FVector2D(vertX * scaleFactor + comp.SectionBaseX,
                        vertY * scaleFactor + comp.SectionBaseY);
                    var textureUv2 = new TIntVector2<int>((int)textureUv.X - minX, (int)textureUv.Y - minY);

                    var weightmapUv = (textureUv - new FVector2D(minX, minY)) * uvScale;

                    heightMapData[textureUv2.X + textureUv2.Y * width] = new L16((ushort)(CDI.GetVertex(vertX, vertY) + relLoc).Z);

                    if (weightMapAllocs != null)
                    {
                        foreach (var allocationInfo in weightMapAllocs)
                        {
                            var weight = CDI.GetLayerWeight(vertX, vertY, allocationInfo);
                            if (weight == 0) continue;

                            var layerName = allocationInfo.GetLayerName();

                            if ((flags & ELandscapeExportFlags.Mesh) == ELandscapeExportFlags.Mesh)
                            {
                                if (!extraVertexColorMap.ContainsKey(layerName))
                                {
                                    var shortName = layerName.SubstringBefore("_LayerInfo");
                                    shortName = shortName.Substring(0, Math.Min(20 - 6, shortName.Length));
                                    extraVertexColorMap.TryAdd(layerName, new CVertexColor(shortName, new FColor[vertexCount]));
                                }

                                extraVertexColorMap[layerName].ColorData[baseVertIndex + vertIndex] = new FColor(weight, weight, weight, weight);
                            }

                            var pixelX = textureUv2.X;
                            var pixelY = textureUv2.Y;

                            if ((flags & ELandscapeExportFlags.Weightmap) == ELandscapeExportFlags.Weightmap)
                            {
                                lock (weightMapLock)
                                {
                                    if (!weightMapsInternal.ContainsKey(layerName))
                                    {
                                        var bitmap = new SKBitmap(width, height, SKColorType.Gray8, SKAlphaType.Unpremul);
                                        weightMapsInternal.TryAdd(layerName, bitmap);
                                        weightMapsPixels.TryAdd(allocationInfo.GetLayerNameHash(), bitmap.GetPixels());
                                    }
                                }

                                unsafe
                                {
                                    var pixels = (byte*)weightMapsPixels[allocationInfo.GetLayerNameHash()];
                                    pixels[pixelY * width + pixelX] = weight;
                                }
                            }
                        }
                    }

                    if ((flags & ELandscapeExportFlags.Mesh) == ELandscapeExportFlags.Mesh && landscapeLod != null)
                    {
                        var vert = landscapeLod.Verts[baseVertIndex + vertIndex];
                        vert.Position = position;
                        vert.Normal = new FVector4(normal);
                        vert.Tangent = tangentX;
                        vert.UV = (FMeshUVFloat)textureUv;

                        if (landscapeLod.ExtraUV?.Value != null && landscapeLod.ExtraUV.Value.Length > 0)
                        {
                            landscapeLod.ExtraUV.Value[0][baseVertIndex + vertIndex] = (FMeshUVFloat)weightmapUv;
                        }
                    }

                    if ((flags & ELandscapeExportFlags.Weightmap) == ELandscapeExportFlags.Weightmap)
                    {
                        lock (weightMapLock)
                        {
                            if (!weightMapsInternal.ContainsKey("NormalMap_DX"))
                            {
                                weightMapsInternal["NormalMap_DX"] = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
                            }

                            var normaBitmap = weightMapsInternal["NormalMap_DX"];
                            unsafe
                            {
                                var pixels = (byte*)normaBitmap.GetPixels();
                                var pixelX = textureUv2.X;
                                var pixelY = textureUv2.Y;
                                var index = pixelY * width + pixelX;
                                pixels[index * 4 + 2] = (byte)(normal.X * 127 + 128);
                                pixels[index * 4 + 1] = (byte)(normal.Y * 127 + 128);
                                pixels[index * 4 + 0] = (byte)(normal.Z * 127 + 128);
                                pixels[index * 4 + 3] = 255;
                            }
                        }
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        if (flags.HasFlag(ELandscapeExportFlags.Heightmap))
        {
            var image = Image.LoadPixelData<L16>(heightMapData, width, height);
            heightMaps.Add("heightmap", image);
        }

        if (flags.HasFlag(ELandscapeExportFlags.Weightmap))
        {
            weightMaps = weightMapsInternal.ToDictionary(x => x.Key, x => x.Value);
        }

        if (!flags.HasFlag(ELandscapeExportFlags.Mesh) || landscapeLod == null)
        {
            return true;
        }

        landscapeLod.ExtraVertexColors = extraVertexColorMap.Values.ToArray();
        extraVertexColorMap.Clear();

        var landscapeMaterial = landscape.LandscapeMaterial;
        var mat = landscapeMaterial?.Load<UMaterialInterface>();
        landscapeLod.Sections = new Lazy<CMeshSection[]>(new[]
        {
            new CMeshSection(0, 0, triangleCount, mat?.Name ?? "默认材质", landscapeMaterial?.ResolvedObject)
        });

        var meshIndices = new List<uint>(triangleCount * 3);
        for (int componentIndex = 0; componentIndex < numComponents; componentIndex++)
        {
            int baseVertIndex = componentIndex * vertexCountPerComponent;

            for (int Y = 0; Y < componentSizeQuads; Y++)
            {
                for (int X = 0; X < componentSizeQuads; X++)
                {
                    var w1 = baseVertIndex + (X + 0) + (Y + 0) * (componentSizeQuads + 1);
                    var w2 = baseVertIndex + (X + 1) + (Y + 1) * (componentSizeQuads + 1);
                    var w3 = baseVertIndex + (X + 1) + (Y + 0) * (componentSizeQuads + 1);

                    meshIndices.Add((uint)w1);
                    meshIndices.Add((uint)w2);
                    meshIndices.Add((uint)w3);

                    var w4 = baseVertIndex + (X + 0) + (Y + 0) * (componentSizeQuads + 1);
                    var w5 = baseVertIndex + (X + 0) + (Y + 1) * (componentSizeQuads + 1);
                    var w6 = baseVertIndex + (X + 1) + (Y + 1) * (componentSizeQuads + 1);

                    meshIndices.Add((uint)w4);
                    meshIndices.Add((uint)w5);
                    meshIndices.Add((uint)w6);
                }
            }
        }

        landscapeLod.Indices = new Lazy<FRawStaticIndexBuffer>(new FRawStaticIndexBuffer { Indices32 = meshIndices.ToArray() });
        meshIndices.Clear();

        convertedMesh = new CStaticMesh();

        FVector min = new(minX, minY, 0);
        FVector max = new(maxX + 1, maxY + 1, Math.Max(maxX - minX, maxY - minY));

        convertedMesh.BoundingBox = new FBox(min, max);

        FVector extent = (max - min) * 0.5f;
        convertedMesh.BoundingSphere = new FSphere(0f, 0f, 0f, extent.Size());

        convertedMesh.LODs.Add(landscapeLod);
        return true;
    }
}