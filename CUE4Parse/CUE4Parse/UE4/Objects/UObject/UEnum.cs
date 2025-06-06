using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System;

namespace CUE4Parse.UE4.Objects.UObject
{
    [SkipObjectRegistration]
    public class UEnum : Assets.Exports.UObject
    {
        public (FName, long)[] Names = Array.Empty<(FName, long)>();

        public ECppForm CppForm;

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            if (Ar.Ver < EUnrealEngineObjectUE4Version.TIGHTLY_PACKED_ENUMS)
            {
                var tempNames = Ar.ReadArray(Ar.ReadFName) ?? Array.Empty<FName>();
                Names = new (FName, long)[tempNames.Length];
                for (var i = 0; i < tempNames.Length; i++)
                {
                    Names[i] = (tempNames[i], i);
                }
            }
            else if (FCoreObjectVersion.Get(Ar) < FCoreObjectVersion.Type.EnumProperties)
            {
                if (Ar.Game == EGame.GAME_腐烂国度2) Ar.Position += 4;

                var oldNames = Ar.ReadArray(() => (Ar.ReadFName(), Ar.Read<byte>())) ?? Array.Empty<(FName, byte)>();
                Names = new (FName, long)[oldNames.Length];
                for (var i = 0; i < oldNames.Length; i++)
                {
                    Names[i] = (oldNames[i].Item1, oldNames[i].Item2);
                }
            }
            else
            {
                Names = Ar.ReadArray(() => (Ar.ReadFName(), Ar.Read<long>())) ?? Array.Empty<(FName, long)>();
            }

            if (Ar.Ver < EUnrealEngineObjectUE4Version.ENUM_CLASS_SUPPORT)
            {
                var bIsNamespace = Ar.ReadBoolean();
                CppForm = bIsNamespace ? ECppForm.Namespaced : ECppForm.Regular;
            }
            else
            {
                CppForm = Ar.Read<ECppForm>();
            }
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName("Names");
            writer.WriteStartObject();
            {
                foreach (var (name, enumValue) in Names)
                {
                    writer.WritePropertyName(name.Text);
                    writer.WriteValue(enumValue);
                }
            }
            writer.WriteEndObject();

            writer.WritePropertyName("CppForm");
            writer.WriteValue(CppForm.ToString());
        }
        public enum ECppForm : byte
        {
            Regular,
            Namespaced,
            EnumClass
        }
    }
}