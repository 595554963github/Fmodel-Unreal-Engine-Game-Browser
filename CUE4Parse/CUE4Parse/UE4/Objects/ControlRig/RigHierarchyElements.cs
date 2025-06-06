using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using System;
using System.Collections.Generic;

namespace CUE4Parse.UE4.Objects.ControlRig
{
    public enum ESerializationPhase
    {
        StaticData,
        InterElementData
    }

    public struct FRigComputedTransform
    {
        public FTransform Transform;
        public bool bDirty;

        public FRigComputedTransform(FAssetArchive Ar)
        {
            Transform = new FTransform(Ar);
            bDirty = Ar.ReadBoolean();
        }
    }

    public struct FRigLocalAndGlobalTransform
    {
        public FRigComputedTransform Local;
        public FRigComputedTransform Global;

        public FRigLocalAndGlobalTransform(FAssetArchive Ar)
        {
            Local = new FRigComputedTransform(Ar);
            Global = new FRigComputedTransform(Ar);
        }
    }

    public struct FRigCurrentAndInitialTransform
    {
        public FRigLocalAndGlobalTransform Current;
        public FRigLocalAndGlobalTransform Initial;

        public FRigCurrentAndInitialTransform(FAssetArchive Ar)
        {
            Current = new FRigLocalAndGlobalTransform(Ar);
            Initial = new FRigLocalAndGlobalTransform(Ar);
        }
    }

    public class FRigBaseElement
    {
        public URigHierarchy? Owner;
        public FRigElementKey LoadedKey;

        public virtual void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            Owner = hierarchy;
            if (serializationPhase != ESerializationPhase.StaticData) return;

            LoadedKey = new FRigElementKey(Ar);

            if (FControlRigObjectVersion.Get(Ar) < FControlRigObjectVersion.Type.HierarchyElementMetadata ||
                FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.RigHierarchyStoresElementMetadata) return;

            var MetadataNum = Ar.Read<int>();
            for (var MetadataIndex = 0; MetadataIndex < MetadataNum; MetadataIndex++)
            {
                FName MetadataName = Ar.ReadFName();
                FName MetadataTypeName = Ar.ReadFName();
                FRigBaseMetadata Md = FRigBoolMetadata.Read(Ar, false);
            }
        }
    }

    public class FRigTransformElement : FRigBaseElement
    {
        public FRigCurrentAndInitialTransform Pose;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);
            if (serializationPhase == ESerializationPhase.StaticData)
                Pose = new FRigCurrentAndInitialTransform(Ar);
        }
    }

    public class FRigSingleParentElement : FRigTransformElement
    {
        public FRigElementKey ParentKey;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);

            if (serializationPhase != ESerializationPhase.InterElementData) return;

            ParentKey = new FRigElementKey(Ar);
        }
    }

    public struct FRigElementWeight
    {
        public float Location;
        public float Rotation;
        public float Scale;

        public FRigElementWeight(float value)
        {
            Location = value;
            Rotation = value;
            Scale = value;
        }
    }

    public struct FRigElementParentConstraint
    {
        public FRigTransformElement ParentElement;
        public FRigElementWeight Weight;
        public FRigElementWeight InitialWeight;
        public bool bCacheIsDirty;
    }

    public class FRigMultiParentElement : FRigTransformElement
    {
        public FRigCurrentAndInitialTransform Parent;
        public FRigElementParentConstraint[] ParentConstraints = Array.Empty<FRigElementParentConstraint>();
        public Dictionary<FRigElementKey, int> IndexLookup = new();

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);
            if (serializationPhase == ESerializationPhase.StaticData)
            {
                if (FControlRigObjectVersion.Get(Ar) < FControlRigObjectVersion.Type.RemovedMultiParentParentCache)
                {
                    Parent = new FRigCurrentAndInitialTransform(Ar);
                }

                var NumParents = Ar.Read<int>();
                ParentConstraints = new FRigElementParentConstraint[NumParents];
            }
            else if (serializationPhase == ESerializationPhase.InterElementData)
            {
                for (var ParentIndex = 0; ParentIndex < ParentConstraints.Length; ParentIndex++)
                {
                    FRigElementParentConstraint constraint = new();
                    FRigElementKey ParentKey = new FRigElementKey(Ar);
                    constraint.bCacheIsDirty = true;

                    if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.RigHierarchyMultiParentConstraints)
                    {
                        constraint.InitialWeight = Ar.Read<FRigElementWeight>();
                        constraint.Weight = Ar.Read<FRigElementWeight>();
                    }
                    else
                    {
                        constraint.InitialWeight = new FRigElementWeight(Ar.Read<float>());
                        constraint.Weight = new FRigElementWeight(Ar.Read<float>());
                    }

                    ParentConstraints[ParentIndex] = constraint;
                    IndexLookup.Add(ParentKey, ParentIndex);
                }
            }
        }
    }

    public enum ERigBoneType
    {
        Imported,
        User
    }

    public class FRigBoneElement : FRigSingleParentElement
    {
        public FName TypeName;
        public ERigBoneType BoneType;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);

            if (serializationPhase != ESerializationPhase.StaticData) return;
            BoneType = EnumUtils.GetValueByName<ERigBoneType>(Ar.ReadFName().Text);
        }
    }

    public enum ERigControlType : byte
    {
        Bool,
        Float,
        Integer,
        Vector2D,
        Position,
        Scale,
        Rotator,
        Transform,
        TransformNoScale,
        EulerTransform,
        ScaleFloat
    }

    public enum ERigControlAxis : byte
    {
        X,
        Y,
        Z
    }

    public enum ERigControlVisibility : byte
    {
        UserDefined,
        BasedOnSelection
    }

    public enum ERigControlTransformChannel : byte
    {
        TranslationX,
        TranslationY,
        TranslationZ,
        Pitch,
        Yaw,
        Roll,
        ScaleX,
        ScaleY,
        ScaleZ
    }

    public enum EEulerRotationOrder : byte
    {
        XYZ,
        XZY,
        YXZ,
        YZX,
        ZXY,
        ZYX
    }

    public struct FRigControlLimitEnabled
    {
        public bool bMinimum;
        public bool bMaximum;

        public FRigControlLimitEnabled(FAssetArchive Ar)
        {
            bMinimum = Ar.ReadBoolean();
            bMaximum = Ar.ReadBoolean();
        }
    }

    public struct FRigControlValue
    {
        public float Float00;
        public float Float01;
        public float Float02;
        public float Float03;
        public float Float10;
        public float Float11;
        public float Float12;
        public float Float13;
        public float Float20;
        public float Float21;
        public float Float22;
        public float Float23;
        public float Float30;
        public float Float31;
        public float Float32;
        public float Float33;
        public float Float00_2;
        public float Float01_2;
        public float Float02_2;
        public float Float03_2;
        public float Float10_2;
        public float Float11_2;
        public float Float12_2;
        public float Float13_2;
        public float Float20_2;
        public float Float21_2;
        public float Float22_2;
        public float Float23_2;
        public float Float30_2;
        public float Float31_2;
        public float Float32_2;
        public float Float33_2;
    }

    public struct FRigControlElementCustomization
    {
        public FRigElementKey[] AvailableSpaces;
        public FRigElementKey[] RemovedSpaces;

        public FRigControlElementCustomization(FAssetArchive Ar)
        {
            AvailableSpaces = Ar.ReadArray(() => new FRigElementKey(Ar));
            RemovedSpaces = Array.Empty<FRigElementKey>();
        }

        public FRigControlElementCustomization(FRigElementKey[] availableSpaces, FRigElementKey[] removedSpaces)
        {
            AvailableSpaces = availableSpaces;
            RemovedSpaces = removedSpaces;
        }
    }

    public struct FRigControlSettings
    {
        public ERigControlType ControlType;
        public FName DisplayName;
        public bool bIsCurve;
        public FRigControlLimitEnabled[] LimitEnabled = Array.Empty<FRigControlLimitEnabled>();
        public bool bDrawLimits;
        public FRigControlValue MinimumValue;
        public FRigControlValue MaximumValue;
        public bool bShapeVisible;
        public FName ShapeName;
        public FLinearColor ShapeColor;
        public bool bIsTransientControl;
        public FRigControlElementCustomization Customization;
        public FRigElementKey[] DrivenControls;
        public FRigElementKey[] PreviouslyDrivenControls;
        public bool bGroupWithParentControl;
        public bool bRestrictSpaceSwitching;
        public ERigControlTransformChannel[] FilteredChannels = Array.Empty<ERigControlTransformChannel>();
        public EEulerRotationOrder PreferredRotationOrder;
        public bool bUsePreferredRotationOrder;

        public FRigControlSettings(FAssetArchive Ar)
        {
            FName ControlTypeName;
            string ControlEnumPathName;
            bool bLimitTranslation_DEPRECATED = false;
            bool bLimitRotation_DEPRECATED = false;
            bool bLimitScale_DEPRECATED = false;

            ControlTypeName = Ar.ReadFName();
            DisplayName = Ar.ReadFName();
            Ar.ReadFName();
            bIsCurve = Ar.ReadBoolean();

            if (FControlRigObjectVersion.Get(Ar) < FControlRigObjectVersion.Type.PerChannelLimits)
            {
                bLimitTranslation_DEPRECATED = Ar.ReadBoolean();
                bLimitRotation_DEPRECATED = Ar.ReadBoolean();
                bLimitScale_DEPRECATED = Ar.ReadBoolean();
            }
            else
            {
                LimitEnabled = Ar.ReadArray(() => new FRigControlLimitEnabled(Ar));
            }

            bDrawLimits = Ar.ReadBoolean();

            FTransform MinimumTransform, MaximumTransform;
            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.StorageMinMaxValuesAsFloatStorage)
            {
                MinimumValue = Ar.Read<FRigControlValue>();
                MaximumValue = Ar.Read<FRigControlValue>();
            }
            else
            {
                MinimumTransform = new FTransform(Ar);
                MaximumTransform = new FTransform(Ar);
            }

            ControlType = EnumUtils.GetValueByName<ERigControlType>(ControlTypeName.Text);

            Ar.ReadBoolean();

            bShapeVisible = Ar.ReadBoolean();

            Ar.ReadFName(); 

            ShapeName = Ar.ReadFName();
            ShapeColor = Ar.Read<FLinearColor>();
            bIsTransientControl = Ar.ReadBoolean();
            ControlEnumPathName = Ar.ReadFString();

            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.RigHierarchyControlSpaceFavorites)
            {
                Customization = new FRigControlElementCustomization(Ar);
            }
            else
            {
                Customization = new FRigControlElementCustomization(Array.Empty<FRigElementKey>(), Array.Empty<FRigElementKey>());
            }

            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.ControlAnimationType)
            {
                DrivenControls = Ar.ReadArray(() => new FRigElementKey(Ar));
            }
            else
            {
                DrivenControls = Array.Empty<FRigElementKey>();
            }

            PreviouslyDrivenControls = Array.Empty<FRigElementKey>();

            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.RestrictSpaceSwitchingForControls)
            {
                bRestrictSpaceSwitching = Ar.ReadBoolean();
            }

            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.ControlTransformChannelFiltering)
            {
                FilteredChannels = Ar.ReadArray(() => Ar.Read<ERigControlTransformChannel>());
            }

            PreferredRotationOrder = EEulerRotationOrder.YZX;
            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.RigHierarchyControlPreferredRotationOrder)
            {
                PreferredRotationOrder = Ar.Read<EEulerRotationOrder>();
            }

            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.RigHierarchyControlPreferredRotationOrderFlag)
            {
                bUsePreferredRotationOrder = Ar.ReadBoolean();
            }
        }
    }

    public struct FRigPreferredEulerAngles
    {
        public EEulerRotationOrder RotationOrder;
        public FVector Current;
        public FVector Initial;

        public FRigPreferredEulerAngles(FAssetArchive Ar)
        {
            RotationOrder = EnumUtils.GetValueByName<EEulerRotationOrder>(Ar.ReadFName().Text);
            Current = new FVector(Ar);
            Initial = new FVector(Ar);
        }
    }

    public class FRigNullElement : FRigMultiParentElement { }

    public class FRigControlElement : FRigMultiParentElement
    {
        public FRigControlSettings Settings;
        public FRigCurrentAndInitialTransform Offset;
        public FRigCurrentAndInitialTransform Shape;
        public FRigPreferredEulerAngles? PreferredEulerAngles;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);

            if (serializationPhase != ESerializationPhase.StaticData) return;

            Settings = new FRigControlSettings(Ar);
            Offset = new FRigCurrentAndInitialTransform(Ar);
            Shape = new FRigCurrentAndInitialTransform(Ar);
            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.PreferredEulerAnglesForControls)
            {
                PreferredEulerAngles = new FRigPreferredEulerAngles(Ar);
            }
        }
    }

    public class FRigCurveElement : FRigBaseElement
    {
        public float Value;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase SerializationPhase)
        {
            base.Load(Ar, hierarchy, SerializationPhase);

            if (SerializationPhase == ESerializationPhase.InterElementData) return;
            var bIsValueSet = Ar.Game >= EGame.GAME_UE5_1 ? Ar.ReadBoolean() : false;
            Value = Ar.Read<float>();
        }
    }

    public struct FRigRigidBodySettings
    {
        public float Mass;

        public FRigRigidBodySettings(FAssetArchive Ar)
        {
            Mass = Ar.Read<float>();
        }
    }

    public class FRigRigidBodyElement : FRigSingleParentElement
    {
        public FRigRigidBodySettings Settings;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);

            if (serializationPhase != ESerializationPhase.StaticData) return;

            Settings = new FRigRigidBodySettings(Ar);
        }
    }

    public class FRigReferenceElement : FRigSingleParentElement { }

    public enum EConnectorType : byte
    {
        Primary,
        Secondary
    }

    public struct FRigConnectionRuleStash
    {
        public string ScriptStructPath;
        public string ExportedText;

        public FRigConnectionRuleStash(FAssetArchive Ar)
        {
            ScriptStructPath = Ar.ReadFString();
            ExportedText = Ar.ReadFString();
        }
    }

    public struct FRigConnectorSettings
    {
        public string Description;
        public EConnectorType Type;
        public bool bOptional;
        public FRigConnectionRuleStash[] Rules;

        public FRigConnectorSettings(FAssetArchive Ar)
        {
            Description = Ar.ReadFString();
            if (FControlRigObjectVersion.Get(Ar) >= FControlRigObjectVersion.Type.ConnectorsWithType)
            {
                Type = Ar.Read<EConnectorType>();
                bOptional = Ar.ReadBoolean();
            }
            else
            {
                Type = EConnectorType.Primary;
                bOptional = false;
            }

            Rules = Ar.ReadArray(() => new FRigConnectionRuleStash(Ar));
        }
    }

    public class FRigConnectorElement : FRigBaseElement
    {
        public FRigConnectorSettings Settings;

        public override void Load(FAssetArchive Ar, URigHierarchy hierarchy, ESerializationPhase serializationPhase)
        {
            base.Load(Ar, hierarchy, serializationPhase);

            if (serializationPhase != ESerializationPhase.StaticData) return;

            Settings = new FRigConnectorSettings(Ar);
        }
    }

    public class FRigSocketElement : FRigSingleParentElement { }

    public enum ERigElementType : byte
    {
        None = 0,
        Bone = 1,
        Null = 2,
        Space = Null,
        Control = 4,
        Curve = 8,
        RigidBody = 16,
        Physics = 16,
        Reference = 32,
        Connector = 64,
        Socket = 128,

        All = Bone | Null | Control | Curve | Physics | Reference | Connector | Socket
    }
}