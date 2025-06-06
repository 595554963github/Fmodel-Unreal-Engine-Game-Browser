using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using FModel.Settings;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace FModel.Extensions
{
    public static class GeneralExtensions
    {
        // ======================
        // 1. 资源加载与分页模块
        // ======================
        private const int DefaultPaginationThreshold = 5000;
        private static readonly Dictionary<string, Lazy<IPackage>> PackageCache = new();

        public class LoadPackageResult
        {
            public IPackage Package { get; set; }
            public int RequestedIndex { get; set; }
            public bool IsPaginated => Package?.ExportMapLength >= DefaultPaginationThreshold;

            public int InclusiveStart => Math.Max(0, RequestedIndex - (RequestedIndex % MaxExportPerPage));
            public int ExclusiveEnd => IsPaginated
                ? Math.Min(InclusiveStart + MaxExportPerPage, Package.ExportMapLength)
                : 0;

            public int MaxExportPerPage { get; set; } = 1;

            public string TabTitleExtra => IsPaginated
                ? $"Exports {InclusiveStart}-{ExclusiveEnd - 1} of {Package.ExportMapLength}"
                : null;

            public IEnumerable<UObject> GetDisplayData(bool showAll = false)
            {
                if (Package == null)
                    return Array.Empty<UObject>();

                if (showAll || !IsPaginated)
                    return Package.GetExports();

                return Package.GetExports(InclusiveStart, ExclusiveEnd - InclusiveStart);
            }
        }

        public static LoadPackageResult LoadWithPagination(
            this IFileProvider provider,
            GameFile file,
            string objectName = null,
            int paginationThreshold = DefaultPaginationThreshold)
        {
            try
            {
                if (provider == null || file == null)
                    return new LoadPackageResult();

                var packagePath = file.Path;
                if (!PackageCache.TryGetValue(packagePath, out var lazyPackage))
                {
                    lazyPackage = new Lazy<IPackage>(() => provider.LoadPackage(file));
                    PackageCache[packagePath] = lazyPackage;
                }

                var package = lazyPackage.Value;
                var result = new LoadPackageResult { Package = package };

                // 解析目标索引
                result.RequestedIndex = package?.GetExportIndex(file.NameWithoutExtension) ?? 0;
                if (!string.IsNullOrEmpty(objectName) && package != null)
                {
                    if (int.TryParse(objectName, out var index))
                    {
                        result.RequestedIndex = index;
                    }
                    else
                    {
                        result.RequestedIndex = package.GetExportIndex(objectName);
                    }
                }

                // 处理世界预览场景
                if (package?.HasFlags(EPackageFlags.PKG_ContainsMap) == true &&
                    UserSettings.Default.PreviewWorlds)
                {
                    result.RequestedIndex = package.GetExportIndex("PersistentLevel");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载包时出错:{ex.Message}");
                return new LoadPackageResult();
            }
        }

        public static void ClearPackageCache() => PackageCache.Clear();

        // ======================
        // 2. 图像处理工具方法
        // ======================
        public static SKBitmap Resize(this SKBitmap bitmap, int width, int height)
        {
            if (bitmap == null) return null;
            return bitmap.Resize(
                new SKImageInfo(width, height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        public static byte[] ToPngBytes(this SKBitmap bitmap)
        {
            if (bitmap == null) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
            return stream.ToArray();
        }
    }
}