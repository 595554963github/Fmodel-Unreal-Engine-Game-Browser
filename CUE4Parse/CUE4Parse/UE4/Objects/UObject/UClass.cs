using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Objects.UObject
{
    [SkipObjectRegistration]
    public class UClass : UStruct
    {
        public bool bCooked;
        public EClassFlags ClassFlags;
        public FPackageIndex ClassWithin = new();
        public FPackageIndex ClassGeneratedBy = new();
        public FName ClassConfigName;
        public FPackageIndex ClassDefaultObject = new();
        public Dictionary<FName, FPackageIndex> FuncMap = new();
        public FImplementedInterface[] Interfaces = Array.Empty<FImplementedInterface>();

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            if (Ar.Game == EGame.GAME_逃出生天) Ar.Position += 4;

            var funcMapNum = Ar.Read<int>();
            FuncMap.Clear();
            for (var i = 0; i < funcMapNum; i++)
            {
                FuncMap[Ar.ReadFName()] = new FPackageIndex(Ar);
            }

            ClassFlags = Ar.Read<EClassFlags>();

            if (Ar.Game is EGame.GAME_星球大战绝地_陨落的武士团 or EGame.GAME_星球大战绝地_幸存者 or EGame.GAME_创世之烬) Ar.Position += 4;
            ClassWithin = new FPackageIndex(Ar);
            ClassConfigName = Ar.ReadFName();
            ClassGeneratedBy = new FPackageIndex(Ar);

            Interfaces = Ar.ReadArray(() => new FImplementedInterface(Ar)) ?? Array.Empty<FImplementedInterface>();

            var bDeprecatedForceScriptOrder = Ar.ReadBoolean();
            var dummy = Ar.ReadFName();

            if (Ar.Ver >= EUnrealEngineObjectUE4Version.ADD_COOKED_TO_UCLASS)
            {
                bCooked = Ar.ReadBoolean();
            }

            ClassDefaultObject = new FPackageIndex(Ar);
        }

        public Assets.Exports.UObject? ConstructObject(EObjectFlags flags)
        {
            var type = ObjectTypeRegistry.Get(Name);
            if (type is null && this is UBlueprintGeneratedClass && flags.HasFlag(EObjectFlags.RF_ClassDefaultObject))
                type = typeof(Assets.Exports.UObject);
            if (type != null)
            {
                try
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance is Assets.Exports.UObject obj)
                    {
                        return obj;
                    }
                    else
                    {
                        Log.Warning("类{Type}确实有一个有效的构造函数，但没有继承UObject", type);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(e, "无法构造类{Type}", type);
                }
            }

            return null;
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            if (FuncMap is { Count: > 0 })
            {
                writer.WritePropertyName("FuncMap");
                serializer.Serialize(writer, FuncMap);
            }

            if (ClassFlags != EClassFlags.CLASS_None)
            {
                writer.WritePropertyName("ClassFlags");
                writer.WriteValue(ClassFlags.ToStringBitfield());
            }

            if (ClassWithin is { IsNull: false })
            {
                writer.WritePropertyName("ClassWithin");
                serializer.Serialize(writer, ClassWithin);
            }

            if (!ClassConfigName.IsNone)
            {
                writer.WritePropertyName("ClassConfigName");
                serializer.Serialize(writer, ClassConfigName);
            }

            if (ClassGeneratedBy is { IsNull: false })
            {
                writer.WritePropertyName("ClassGeneratedBy");
                serializer.Serialize(writer, ClassGeneratedBy);
            }

            if (Interfaces is { Length: > 0 })
            {
                writer.WritePropertyName("Interfaces");
                serializer.Serialize(writer, Interfaces);
            }

            if (bCooked)
            {
                writer.WritePropertyName("bCooked");
                writer.WriteValue(bCooked);
            }

            if (ClassDefaultObject is { IsNull: false })
            {
                writer.WritePropertyName("ClassDefaultObject");
                serializer.Serialize(writer, ClassDefaultObject);
            }
        }

        public class FImplementedInterface
        {
            public FPackageIndex Class;
            public int PointerOffset;
            public bool bImplementedByK2;

            public FImplementedInterface(FAssetArchive Ar)
            {
                Class = new FPackageIndex(Ar);
                PointerOffset = Ar.Read<int>();
                bImplementedByK2 = Ar.ReadBoolean();
            }
        }
    }
}