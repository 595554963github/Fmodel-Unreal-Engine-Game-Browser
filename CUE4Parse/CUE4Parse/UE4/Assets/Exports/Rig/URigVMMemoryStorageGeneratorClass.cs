using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.RigVM;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;
using System;

namespace CUE4Parse.UE4.Assets.Exports.Rig;

public class URigVMMemoryStorageGeneratorClass : UClass
{
    public ERigVMMemoryType MemoryType { get; set; }
    // A list of descriptions for the property paths - used for serialization
    public FRigVMPropertyPathDescription[] PropertyPathDescriptions { get; set; } = Array.Empty<FRigVMPropertyPathDescription>();

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        PropertyPathDescriptions = Ar.ReadArray(() => new FRigVMPropertyPathDescription(Ar));
        MemoryType = Ar.Read<ERigVMMemoryType>();
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);
        writer.WritePropertyName(nameof(MemoryType));
        serializer.Serialize(writer, MemoryType);
        writer.WritePropertyName(nameof(PropertyPathDescriptions));
        serializer.Serialize(writer, PropertyPathDescriptions);
    }
}