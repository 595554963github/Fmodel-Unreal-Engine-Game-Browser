using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;

namespace CUE4Parse.UE4.Assets.Exports.Animation
{
    public class USkeleton : UObject
    {
        public EBoneTranslationRetargetingMode[] BoneTree { get; private set; } = Array.Empty<EBoneTranslationRetargetingMode>();
        public FReferenceSkeleton ReferenceSkeleton { get; private set; }
        public FGuid Guid { get; private set; } = new FGuid();
        public FGuid VirtualBoneGuid { get; private set; } = new FGuid();
        public Dictionary<FName, FReferencePose> AnimRetargetSources { get; private set; } = new Dictionary<FName, FReferencePose>();
        public Dictionary<FName, FSmartNameMapping> NameMappings { get; private set; } = new Dictionary<FName, FSmartNameMapping>();
        public FName[] ExistingMarkerNames { get; private set; } = Array.Empty<FName>();
        public FPackageIndex[] Sockets { get; private set; } = Array.Empty<FPackageIndex>();
        public FVirtualBone[] VirtualBones { get; private set; } = Array.Empty<FVirtualBone>();

        public int BoneCount => ReferenceSkeleton?.FinalRefBoneInfo?.Length ?? 0;

        public USkeleton()
        {
            var emptyArchive = CreateEmptyArchive();
            ReferenceSkeleton = new FReferenceSkeleton(emptyArchive);
        }

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.REFERENCE_SKELETON_REFACTOR)
            {
                ReferenceSkeleton = new FReferenceSkeleton(Ar);
            }

            if (TryGetValue(out FStructFallback[] boneTree, nameof(BoneTree)))
            {
                BoneTree = new EBoneTranslationRetargetingMode[boneTree.Length];
                for (var i = 0; i < BoneTree.Length; i++)
                {
                    BoneTree[i] = boneTree[i].GetOrDefault<EBoneTranslationRetargetingMode>("TranslationRetargetingMode");
                }
            }

            VirtualBoneGuid = GetOrDefault(nameof(VirtualBoneGuid), new FGuid());
            Sockets = GetOrDefault(nameof(Sockets), Array.Empty<FPackageIndex>());
            VirtualBones = GetOrDefault(nameof(VirtualBones), Array.Empty<FVirtualBone>());

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.FIX_ANIMATIONBASEPOSE_SERIALIZATION)
            {
                var numOfRetargetSources = Ar.Read<int>();
                AnimRetargetSources = new Dictionary<FName, FReferencePose>(numOfRetargetSources);
                for (var i = 0; i < numOfRetargetSources; i++)
                {
                    var name = Ar.ReadFName();
                    var pose = new FReferencePose(Ar);
                    ReferenceSkeleton?.AdjustBoneScales(pose.ReferencePose);
                    AnimRetargetSources[name] = pose;
                }
            }
            else
            {
                Log.Warning("不支持旧版动画基本姿势序列化格式");
            }

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.SKELETON_GUID_SERIALIZATION)
            {
                Guid = Ar.Read<FGuid>();
            }

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.SKELETON_ADD_SMARTNAMES)
            {
                var mapLength = Ar.Read<int>();
                NameMappings = new Dictionary<FName, FSmartNameMapping>(mapLength);
                for (var i = 0; i < mapLength; i++)
                {
                    NameMappings[Ar.ReadFName()] = new FSmartNameMapping(Ar);
                }
            }

            if (FAnimObjectVersion.Get(Ar) >= FAnimObjectVersion.Type.StoreMarkerNamesOnSkeleton)
            {
                var stripDataFlags = Ar.Read<FStripDataFlags>();
                if (!stripDataFlags.IsEditorDataStripped())
                {
                    ExistingMarkerNames = Ar.ReadArray(Ar.ReadFName);
                }
            }
        }

        private FAssetArchive CreateEmptyArchive()
        {
            var emptyArchive = new FByteArchive("EmptyArchive", Array.Empty<byte>());
            var package = this.Owner;
            return new FAssetArchive(emptyArchive, package);
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName("ReferenceSkeleton");
            serializer.Serialize(writer, ReferenceSkeleton);

            writer.WritePropertyName("Guid");
            serializer.Serialize(writer, Guid);

            writer.WritePropertyName("AnimRetargetSources");
            serializer.Serialize(writer, AnimRetargetSources);

            writer.WritePropertyName("NameMappings");
            serializer.Serialize(writer, NameMappings);

            writer.WritePropertyName("ExistingMarkerNames");
            serializer.Serialize(writer, ExistingMarkerNames);
        }
    }
}

[StructFallback]
public class FVirtualBone
{
    public FName SourceBoneName { get; private set; } = new FName();
    public FName TargetBoneName { get; private set; } = new FName();
    public FName VirtualBoneName { get; private set; } = new FName();

    public FVirtualBone(FStructFallback fallback)
    {
        SourceBoneName = fallback.GetOrDefault(nameof(SourceBoneName), new FName());
        TargetBoneName = fallback.GetOrDefault(nameof(TargetBoneName), new FName());
        VirtualBoneName = fallback.GetOrDefault(nameof(VirtualBoneName), new FName());
    }
}