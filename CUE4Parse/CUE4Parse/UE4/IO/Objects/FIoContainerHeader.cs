using System;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.IO.Objects
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FIoContainerHeaderLocalizedPackage
    {
        public readonly FPackageId SourcePackageId;
        public readonly FMappedName SourcePackageName;

        public FIoContainerHeaderLocalizedPackage(FArchive Ar)
        {
            SourcePackageId = Ar.Read<FPackageId>();
            SourcePackageName = Ar.Read<FMappedName>();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FIoContainerHeaderPackageRedirect
    {
        public readonly FPackageId SourcePackageId;
        public readonly FPackageId TargetPackageId;
        public readonly FMappedName SourcePackageName;

        public FIoContainerHeaderPackageRedirect(FArchive Ar)
        {
            SourcePackageId = Ar.Read<FPackageId>();
            TargetPackageId = Ar.Read<FPackageId>();
            SourcePackageName = Ar.Read<FMappedName>();
        }
    }

    public readonly struct FIoContainerHeaderSoftPackageReferences
    {
        public readonly FPackageId[] PackageIds;
        public readonly byte[] PackageIndices;
        public readonly bool bContainsSoftPackageReferences;

        public FIoContainerHeaderSoftPackageReferences(FArchive Ar)
        {
            bContainsSoftPackageReferences = Ar.ReadBoolean();
            if (bContainsSoftPackageReferences)
            {
                PackageIds = Ar.ReadArray<FPackageId>();
                PackageIndices = Ar.ReadArray<byte>();
            }
            else
            {
                PackageIds = Array.Empty<FPackageId>();
                PackageIndices = Array.Empty<byte>();
            }
        }
    }

    public enum EIoContainerHeaderVersion
    {
        BeforeVersionWasAdded = -1,
        Initial = 0,
        LocalizedPackages = 1,
        OptionalSegmentPackages = 2,
        NoExportInfo = 3,
        SoftPackageReferences = 4,
        SoftPackageReferencesOffset = 5,
        LatestPlusOne,
        Latest = LatestPlusOne - 1
    }

    public class FIoContainerHeader
    {
        private const int Signature = 0x496f436e;
        public readonly EIoContainerHeaderVersion Version;
        public readonly FIoContainerId ContainerId;

        public FPackageId[] PackageIds { get; } = Array.Empty<FPackageId>();
        public FFilePackageStoreEntry[] StoreEntries { get; } = Array.Empty<FFilePackageStoreEntry>();
        public FFilePackageStoreEntry[] OptionalSegmentStoreEntries { get; } = Array.Empty<FFilePackageStoreEntry>();
        public FPackageId[] OptionalSegmentPackageIds { get; } = Array.Empty<FPackageId>();

        public FNameEntrySerialized[]? ContainerNameMap { get; }
        public FIoContainerHeaderLocalizedPackage[]? LocalizedPackages { get; }
        public FIoContainerHeaderPackageRedirect[] PackageRedirects { get; } = Array.Empty<FIoContainerHeaderPackageRedirect>();
        public FIoContainerHeaderSerialInfo SoftPackageReferencesSerialInfo { get; }
        public FIoContainerHeaderSoftPackageReferences SoftPackageReferences { get; }

        public FIoContainerHeader(FArchive Ar)
        {
            Version = Ar.Game >= EGame.GAME_UE5_0 ? EIoContainerHeaderVersion.Initial : EIoContainerHeaderVersion.BeforeVersionWasAdded;
            if (Version == EIoContainerHeaderVersion.Initial)
            {
                var signature = Ar.Read<uint>();
                if (signature != Signature)
                {
                    throw new ParserException(Ar, $"容器头部签名无效: 0x{signature:X8} != 0x{Signature:X8}");
                }

                Version = Ar.Read<EIoContainerHeaderVersion>();
            }

            ContainerId = Ar.Read<FIoContainerId>();
            var packageCount = Version < EIoContainerHeaderVersion.OptionalSegmentPackages ? Ar.Read<uint>() : 0;
            if (Version == EIoContainerHeaderVersion.BeforeVersionWasAdded)
            {
                var namesSize = Ar.Read<int>();
                var namesPos = Ar.Position;
                var nameHashesSize = Ar.Read<int>();
                var continuePos = Ar.Position + nameHashesSize;
                if (namesSize > 0 && nameHashesSize > 0)
                {
                    Ar.Position = namesPos;
                    ContainerNameMap = FNameEntrySerialized.LoadNameBatch(Ar, nameHashesSize / sizeof(ulong) - 1);
                }
                Ar.Position = continuePos;
            }

            ReadPackageIdsAndEntries(Ar, out var packageIds, out var storeEntries);
            PackageIds = packageIds;
            StoreEntries = storeEntries;

            if (Version >= EIoContainerHeaderVersion.OptionalSegmentPackages)
            {
                ReadPackageIdsAndEntries(Ar, out var optionalSegmentPackageIds, out var optionalSegmentStoreEntries);
                OptionalSegmentPackageIds = optionalSegmentPackageIds;
                OptionalSegmentStoreEntries = optionalSegmentStoreEntries;
            }
            if (Version >= EIoContainerHeaderVersion.Initial)
            {
                ContainerNameMap = FNameEntrySerialized.LoadNameBatch(Ar);
            }
            if (Version >= EIoContainerHeaderVersion.LocalizedPackages)
            {
                LocalizedPackages = Ar.ReadArray<FIoContainerHeaderLocalizedPackage>();
            }
            PackageRedirects = Ar.ReadArray<FIoContainerHeaderPackageRedirect>();
            if (Version == EIoContainerHeaderVersion.SoftPackageReferences)
            {
                SoftPackageReferences = new FIoContainerHeaderSoftPackageReferences(Ar);
                var tempAr = new FByteArchive("temp", Array.Empty<byte>());
                SoftPackageReferencesSerialInfo = new FIoContainerHeaderSerialInfo(tempAr);
            }
            else if (Version >= EIoContainerHeaderVersion.SoftPackageReferencesOffset)
            {
                SoftPackageReferencesSerialInfo = new FIoContainerHeaderSerialInfo(Ar);
            }
            else
            {
                var tempAr = new FByteArchive("temp", Array.Empty<byte>());
                SoftPackageReferencesSerialInfo = new FIoContainerHeaderSerialInfo(tempAr);
            }
        }

        private void ReadPackageIdsAndEntries(FArchive Ar, out FPackageId[] packageIds, out FFilePackageStoreEntry[] storeEntries)
        {
            packageIds = Ar.ReadArray<FPackageId>();
            var storeEntriesSize = Ar.Read<int>();
            var storeEntriesEnd = Ar.Position + storeEntriesSize;
            storeEntries = Ar.ReadArray(packageIds.Length, () => new FFilePackageStoreEntry(Ar, Version));
            Ar.Position = storeEntriesEnd;
        }
    }
}