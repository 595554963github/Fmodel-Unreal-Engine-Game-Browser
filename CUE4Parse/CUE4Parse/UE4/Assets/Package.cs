using CUE4Parse.FileProvider;
using CUE4Parse.GameTypes.ACE7.Encryption;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CUE4Parse.UE4.Assets
{
    [SkipObjectRegistration]
    public sealed class Package : AbstractUePackage
    {
        public override FPackageFileSummary Summary { get; }
        public override FNameEntrySerialized[] NameMap { get; }
        public override int ImportMapLength => ImportMap.Length;
        public override int ExportMapLength => ExportMap.Length;

        public FObjectImport[] ImportMap { get; }
        public FObjectExport[] ExportMap { get; }
        public FPackageIndex[][]? DependsMap { get; }
        public FPackageIndex[]? PreloadDependencies { get; }
        public FObjectDataResource[]? DataResourceMap { get; }

        private ExportLoader[]? _exportLoaders;

        public Package(FArchive uasset, FArchive? uexp, FArchive? ubulk = null, FArchive? uptnl = null, IFileProvider? provider = null, bool useLazySerialization = true)
            : this(
                uasset,
                uexp,
                ubulk != null ? new Lazy<FArchive?>(() => ubulk) : null,
                uptnl != null ? new Lazy<FArchive?>(() => uptnl) : null,
                provider,
                useLazySerialization)
        { }

        public Package(string name, byte[] uasset, byte[]? uexp, byte[]? ubulk = null, byte[]? uptnl = null, IFileProvider? provider = null, bool useLazySerialization = true)
            : this(
                new FByteArchive($"{name}.uasset", uasset),
                uexp != null ? new FByteArchive($"{name}.uexp", uexp) : null,
                ubulk != null ? new FByteArchive($"{name}.ubulk", ubulk) : null,
                uptnl != null ? new FByteArchive($"{name}.uptnl", uptnl) : null,
                provider,
                useLazySerialization)
        { }

        public Package(
            FArchive uasset,
            FArchive? uexp,
            Lazy<FArchive?>? ubulk = null,
            Lazy<FArchive?>? uptnl = null,
            IFileProvider? provider = null,
            bool useLazySerialization = true)
            : base(uasset.Name.SubstringBeforeLast('.'), provider)
        {
            uasset.Versions = (VersionContainer)uasset.Versions.Clone();

            FAssetArchive uassetAr;
            ACE7XORKey? xorKey = null;
            ACE7Decrypt? decryptor = null;
            if (uasset.Game == EGame.GAME_皇牌空战7)
            {
                decryptor = new ACE7Decrypt();
                uassetAr = new FAssetArchive(decryptor.DecryptUassetArchive(uasset, out xorKey), this);
            }
            else
            {
                uassetAr = new FAssetArchive(uasset, this);
            }

            Summary = new FPackageFileSummary(uassetAr);

            uassetAr.SeekAbsolute(Summary.NameOffset, SeekOrigin.Begin);
            NameMap = new FNameEntrySerialized[Summary.NameCount];
            uassetAr.ReadArray(NameMap, () => new FNameEntrySerialized(uassetAr));

            uassetAr.SeekAbsolute(Summary.ImportOffset, SeekOrigin.Begin);
            ImportMap = new FObjectImport[Summary.ImportCount];
            uassetAr.ReadArray(ImportMap, () => new FObjectImport(uassetAr));

            uassetAr.SeekAbsolute(Summary.ExportOffset, SeekOrigin.Begin);
            ExportMap = new FObjectExport[Summary.ExportCount];
            ExportsLazy = new Lazy<UObject>[Summary.ExportCount];
            uassetAr.ReadArray(ExportMap, () => new FObjectExport(uassetAr));

            if (!useLazySerialization && Summary is { DependsOffset: > 0, ExportCount: > 0 })
            {
                uassetAr.SeekAbsolute(Summary.DependsOffset, SeekOrigin.Begin);
                DependsMap = uassetAr.ReadArray(Summary.ExportCount, () => uassetAr.ReadArray(() => new FPackageIndex(uassetAr)));
            }

            if (!useLazySerialization && Summary is { PreloadDependencyCount: > 0, PreloadDependencyOffset: > 0 })
            {
                uassetAr.SeekAbsolute(Summary.PreloadDependencyOffset, SeekOrigin.Begin);
                PreloadDependencies = uassetAr.ReadArray(Summary.PreloadDependencyCount, () => new FPackageIndex(uassetAr));
            }

            if (Summary.DataResourceOffset > 0)
            {
                uassetAr.SeekAbsolute(Summary.DataResourceOffset, SeekOrigin.Begin);
                var dataResourceVersion = (EObjectDataResourceVersion)uassetAr.Read<uint>();
                if (dataResourceVersion is > EObjectDataResourceVersion.Invalid and <= EObjectDataResourceVersion.Latest)
                {
                    DataResourceMap = uassetAr.ReadArray(() => new FObjectDataResource(uassetAr, dataResourceVersion));
                }
            }

            if (!CanDeserialize) return;

            FAssetArchive uexpAr;
            if (uexp != null)
            {
                if (uasset.Game == EGame.GAME_皇牌空战7 && decryptor != null && xorKey != null)
                {
                    uexpAr = new FAssetArchive(decryptor.DecryptUexpArchive(uexp, xorKey), this, (int)uassetAr.Length);
                }
                else
                {
                    uexpAr = new FAssetArchive(uexp, this, (int)uassetAr.Length);
                }
            }
            else
            {
                uexpAr = uassetAr;
            }

            if (ubulk != null)
            {
                var offset = Summary.BulkDataStartOffset;
                uexpAr.AddPayload(PayloadType.UBULK, offset, ubulk);
            }

            if (uptnl != null)
            {
                var offset = Summary.BulkDataStartOffset;
                uexpAr.AddPayload(PayloadType.UPTNL, offset, uptnl);
            }

            if (useLazySerialization)
            {
                for (var i = 0; i < ExportsLazy.Length; i++)
                {
                    var export = ExportMap[i];
                    ExportsLazy[i] = new Lazy<UObject>(() =>
                    {
                        var classObj = ResolvePackageIndex(export.ClassIndex)?.Object?.Value as UStruct;
                        var obj = ConstructObject(classObj, this, (EObjectFlags)export.ObjectFlags);
                        if (obj == null)
                        {
                            throw new InvalidOperationException("构造对象失败");
                        }

                        obj.Name = export.ObjectName.Text;
                        var outer = ResolvePackageIndex(export.OuterIndex) as ResolvedExportObject;
                        obj.Outer = outer?.Object?.Value ?? this;
                        obj.Super = ResolvePackageIndex(export.SuperIndex) as ResolvedExportObject;
                        obj.Template = ResolvePackageIndex(export.TemplateIndex) as ResolvedExportObject;
                        obj.Flags |= (EObjectFlags)export.ObjectFlags;

                        var Ar = (FAssetArchive)uexpAr.Clone();
                        Ar.SeekAbsolute(export.SerialOffset, SeekOrigin.Begin);
                        DeserializeObject(obj, Ar, export.SerialSize);
                        obj.Flags |= EObjectFlags.RF_LoadCompleted;
                        obj.PostLoad();
                        return obj;
                    });
                }
            }
            else
            {
                _exportLoaders = new ExportLoader[ExportMap.Length];
                for (var i = 0; i < ExportMap.Length; i++)
                {
                    _exportLoaders[i] = new ExportLoader(this, i, uexpAr);
                }
            }

            IsFullyLoaded = true;
        }

        public override int GetExportIndex(string name, StringComparison comparisonType = StringComparison.Ordinal)
        {
            for (var i = 0; i < ExportMap.Length; i++)
            {
                if (ExportMap[i].ObjectName.Text.Equals(name, comparisonType))
                {
                    return i;
                }
            }

            return -1;
        }

        public override ResolvedObject? ResolvePackageIndex(FPackageIndex? index)
        {
            if (index == null || index.IsNull)
                return null;
            if (index.IsImport && -index.Index - 1 < ImportMap.Length)
                return ResolveImport(index);
            if (index.IsExport && index.Index - 1 < ExportMap.Length)
                return new ResolvedExportObject(index.Index - 1, this);
            return null;
        }

        private ResolvedObject? ResolveImport(FPackageIndex importIndex)
        {
            var import = ImportMap[-importIndex.Index - 1];
            var outerMostIndex = importIndex;
            FObjectImport outerMostImport;
            while (true)
            {
                outerMostImport = ImportMap[-outerMostIndex.Index - 1];
                if (outerMostImport.OuterIndex == null || outerMostImport.OuterIndex.IsNull)
                    break;
                outerMostIndex = outerMostImport.OuterIndex;
            }

            outerMostImport = ImportMap[-outerMostIndex.Index - 1];
            if (outerMostImport.ObjectName.Text.StartsWith("/Script/"))
            {
                return new ResolvedImportObject(import, this);
            }

            if (Provider == null)
                return null;

            Package? importPackage = null;
            if (Provider.TryLoadPackage(outerMostImport.ObjectName.Text, out var package))
                importPackage = package as Package;
            if (importPackage == null)
            {
                return new ResolvedImportObject(import, this);
            }

            string? outer = null;
            if (outerMostIndex != import.OuterIndex && import.OuterIndex != null && import.OuterIndex.IsImport)
            {
                var outerImport = ImportMap[-import.OuterIndex.Index - 1];
                var resolvedOuter = ResolveImport(import.OuterIndex);
                outer = resolvedOuter?.GetPathName();
                if (outer == null)
                {
                    return new ResolvedImportObject(import, this);
                }
            }

            for (var i = 0; i < importPackage.ExportMap.Length; i++)
            {
                var export = importPackage.ExportMap[i];
                if (export.ObjectName.Text != import.ObjectName.Text)
                    continue;
                var thisOuter = importPackage.ResolvePackageIndex(export.OuterIndex);
                if (thisOuter?.GetPathName() == outer)
                    return new ResolvedExportObject(i, importPackage);
            }

            return new ResolvedImportObject(import, this);
        }

        private class ResolvedExportObject : ResolvedObject
        {
            private readonly FObjectExport _export;

            public ResolvedExportObject(int exportIndex, Package package) : base(package, exportIndex)
            {
                _export = package.ExportMap[exportIndex] ?? throw new ArgumentNullException(nameof(exportIndex));
            }

            public override FName Name => _export?.ObjectName ?? "None";
            public override ResolvedObject Outer => Package.ResolvePackageIndex(_export.OuterIndex) ?? new ResolvedLoadedObject((UObject)Package);
            public override ResolvedObject? Class => Package.ResolvePackageIndex(_export.ClassIndex);
            public override ResolvedObject? Super => Package.ResolvePackageIndex(_export.SuperIndex);
        }

        private class ResolvedImportObject : ResolvedObject
        {
            private readonly FObjectImport _import;

            public ResolvedImportObject(FObjectImport import, Package package) : base(package)
            {
                _import = import ?? throw new ArgumentNullException(nameof(import));
            }

            public override FName Name => _import.ObjectName;
            public override ResolvedObject? Outer => Package.ResolvePackageIndex(_import.OuterIndex);
            public override ResolvedObject Class => new ResolvedLoadedObject(new UScriptClass(_import.ClassName.Text));
            public override Lazy<UObject>? Object => _import.ClassName.Text switch
            {
                "Class" => new(() => new UScriptClass(Name.Text)),
                "SharpClass" => new(() => new USharpClass(Name.Text)),
                "PythonClass" => new(() => new UPythonClass(Name.Text)),
                "ASClass" => new(() => new UASClass(Name.Text)),
                "ScriptStruct" => new(() => new UScriptClass(Name.Text)),
                _ => null
            };
        }

        public class UASClass : UScriptClass
        {
            public UASClass(string name) : base(name)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
            }
        }

        private class ExportLoader
        {
            private readonly Package _package;
            private readonly FObjectExport _export;
            private readonly FAssetArchive _archive;
            private UObject? _object;
            private List<LoadDependency>? _dependencies;
            private LoadPhase _phase = LoadPhase.Create;
            public Lazy<UObject> Lazy;

            public ExportLoader(Package package, int index, FAssetArchive archive)
            {
                _package = package ?? throw new ArgumentNullException(nameof(package));
                _export = package.ExportMap[index] ?? throw new ArgumentNullException(nameof(index));
                _archive = archive ?? throw new ArgumentNullException(nameof(archive));

                Lazy = new Lazy<UObject>(() =>
                {
                    Fire(LoadPhase.Serialize);
                    return _object ?? throw new InvalidOperationException("对象未初始化");
                });
                package.ExportsLazy[index] = Lazy;
            }

            private void EnsureDependencies()
            {
                if (_dependencies != null)
                {
                    return;
                }

                _dependencies = new List<LoadDependency>();
                var runningIndex = _export.FirstExportDependency;
                if (runningIndex >= 0 && _package.PreloadDependencies != null)
                {
                    for (var index = _export.SerializationBeforeSerializationDependencies; index > 0; index--)
                    {
                        if (runningIndex < _package.PreloadDependencies.Length)
                        {
                            var dep = _package.PreloadDependencies[runningIndex++];
                            if (dep != null)
                            {
                                _dependencies.Add(new LoadDependency(LoadPhase.Serialize, LoadPhase.Serialize, ResolveLoader(dep)));
                            }
                        }
                    }
                    for (var index = _export.CreateBeforeSerializationDependencies; index > 0; index--)
                    {
                        if (runningIndex < _package.PreloadDependencies.Length)
                        {
                            var dep = _package.PreloadDependencies[runningIndex++];
                            if (dep != null)
                            {
                                _dependencies.Add(new LoadDependency(LoadPhase.Serialize, LoadPhase.Create, ResolveLoader(dep)));
                            }
                        }
                    }
                    for (var index = _export.SerializationBeforeCreateDependencies; index > 0; index--)
                    {
                        if (runningIndex < _package.PreloadDependencies.Length)
                        {
                            var dep = _package.PreloadDependencies[runningIndex++];
                            if (dep != null)
                            {
                                _dependencies.Add(new LoadDependency(LoadPhase.Create, LoadPhase.Serialize, ResolveLoader(dep)));
                            }
                        }
                    }
                    for (var index = _export.CreateBeforeCreateDependencies; index > 0; index--)
                    {
                        if (runningIndex < _package.PreloadDependencies.Length)
                        {
                            var dep = _package.PreloadDependencies[runningIndex++];
                            if (dep != null)
                            {
                                _dependencies.Add(new LoadDependency(LoadPhase.Create, LoadPhase.Create, ResolveLoader(dep)));
                            }
                        }
                    }
                }
                else
                {
                    if (_export.OuterIndex != null)
                    {
                        _dependencies.Add(new LoadDependency(LoadPhase.Create, LoadPhase.Create, ResolveLoader(_export.OuterIndex)));
                    }
                }
            }

            private ExportLoader? ResolveLoader(FPackageIndex index)
            {
                if (index == null || !index.IsExport || _package._exportLoaders == null)
                    return null;

                var exportIndex = index.Index - 1;
                if (exportIndex >= 0 && exportIndex < _package._exportLoaders.Length)
                {
                    return _package._exportLoaders[exportIndex];
                }
                return null;
            }

            private void Fire(LoadPhase untilPhase)
            {
                if (untilPhase >= LoadPhase.Create && _phase <= LoadPhase.Create)
                {
                    FireDependencies(LoadPhase.Create);
                    Create();
                }
                if (untilPhase >= LoadPhase.Serialize && _phase <= LoadPhase.Serialize)
                {
                    FireDependencies(LoadPhase.Serialize);
                    Serialize();
                }
            }

            private void FireDependencies(LoadPhase phase)
            {
                EnsureDependencies();
                foreach (var dependency in _dependencies ?? Enumerable.Empty<LoadDependency>())
                {
                    if (dependency.FromPhase == phase)
                    {
                        dependency.Target?.Fire(dependency.ToPhase);
                    }
                }
            }

            private void Create()
            {
                Trace.Assert(_phase == LoadPhase.Create);
                _phase = LoadPhase.Serialize;

                var classObj = _package.ResolvePackageIndex(_export.ClassIndex)?.Object?.Value as UStruct;
                _object = _package.ConstructObject(classObj, _package, (EObjectFlags)_export.ObjectFlags);
                if (_object == null)
                {
                    throw new InvalidOperationException("构造对象失败");
                }

                _object.Name = _export.ObjectName.Text;

                if (_export.OuterIndex != null && !_export.OuterIndex.IsNull && _export.OuterIndex.IsExport &&
    _package._exportLoaders != null && _export.OuterIndex.Index - 1 < _package._exportLoaders.Length)
                {
                    _object.Outer = _package._exportLoaders[_export.OuterIndex.Index - 1]._object ?? _package;
                }
                else
                {
                    _object.Outer = _package;
                }

                _object.Super = _package.ResolvePackageIndex(_export.SuperIndex) as ResolvedExportObject;
                _object.Template = _package.ResolvePackageIndex(_export.TemplateIndex) as ResolvedExportObject;
                _object.Flags |= (EObjectFlags)_export.ObjectFlags;
            }

            private void Serialize()
            {
                Trace.Assert(_phase == LoadPhase.Serialize);
                _phase = LoadPhase.Complete;

                var Ar = (FAssetArchive)_archive.Clone();
                Ar.SeekAbsolute(_export.SerialOffset, SeekOrigin.Begin);
                _package.DeserializeObject(_object!, Ar, _export.SerialSize);
                _object!.Flags |= EObjectFlags.RF_LoadCompleted;
                _object.PostLoad();
            }
        }

        private class LoadDependency
        {
            public LoadPhase FromPhase { get; }
            public LoadPhase ToPhase { get; }
            public ExportLoader? Target { get; }

            public LoadDependency(LoadPhase fromPhase, LoadPhase toPhase, ExportLoader? target)
            {
                FromPhase = fromPhase;
                ToPhase = toPhase;
                Target = target;
            }
        }

        private enum LoadPhase
        {
            Create,
            Serialize,
            Complete
        }
    }
}