using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;
using System;

namespace CUE4Parse.UE4.Objects.Engine
{
    public class UWorld : Assets.Exports.UObject
    {
        public FPackageIndex PersistentLevel { get; private set; } = new FPackageIndex();
        public FPackageIndex[] ExtraReferencedObjects { get; private set; } = Array.Empty<FPackageIndex>();
        public FPackageIndex[] StreamingLevels { get; private set; } = Array.Empty<FPackageIndex>();

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            PersistentLevel = new FPackageIndex(Ar);
            ExtraReferencedObjects = Ar.ReadArray(() => new FPackageIndex(Ar)) ?? Array.Empty<FPackageIndex>();
            StreamingLevels = Ar.ReadArray(() => new FPackageIndex(Ar)) ?? Array.Empty<FPackageIndex>();
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName("PersistentLevel");
            serializer.Serialize(writer, PersistentLevel);

            writer.WritePropertyName("ExtraReferencedObjects");
            serializer.Serialize(writer, ExtraReferencedObjects);

            writer.WritePropertyName("StreamingLevels");
            serializer.Serialize(writer, StreamingLevels);
        }
    }
}