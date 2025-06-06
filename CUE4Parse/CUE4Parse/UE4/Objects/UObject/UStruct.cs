using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Objects.UObject;

[SkipObjectRegistration]
public class UStruct : UField
{
    public FPackageIndex SuperStruct = new();
    public FPackageIndex[] Children = Array.Empty<FPackageIndex>();
    public FField[] ChildProperties = Array.Empty<FField>();
    public KismetExpression[] ScriptBytecode = Array.Empty<KismetExpression>();

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        SuperStruct = new FPackageIndex(Ar);

        if (FFrameworkObjectVersion.Get(Ar) < FFrameworkObjectVersion.Type.RemoveUField_Next)
        {
            var firstChild = new FPackageIndex(Ar);
            Children = firstChild.IsNull
                ? Array.Empty<FPackageIndex>()
                : new[] { firstChild };
        }
        else
        {
            Children = Ar.ReadArray(() => new FPackageIndex(Ar)) ?? Array.Empty<FPackageIndex>();
        }

        if (FCoreObjectVersion.Get(Ar) >= FCoreObjectVersion.Type.FProperties)
        {
            DeserializeProperties(Ar);
        }

        var bytecodeBufferSize = Ar.Read<int>();
        var serializedScriptSize = Ar.Read<int>();

        if (Ar.Owner!.Provider?.ReadScriptData == true && serializedScriptSize > 0)
        {
            using var kismetAr = new FKismetArchive(Name, Ar.ReadBytes(serializedScriptSize), Ar.Owner, Ar.Versions);
            var tempCode = new List<KismetExpression>();
            try
            {
                while (kismetAr.Position < kismetAr.Length)
                {
                    tempCode.Add(kismetAr.ReadExpression());
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, $"未能序列化脚本字节码{Name}");
            }
            finally
            {
                ScriptBytecode = tempCode.ToArray();
            }
        }
        else
        {
            Ar.Position += serializedScriptSize;
        }
    }

    protected void DeserializeProperties(FAssetArchive Ar)
    {
        ChildProperties = Ar.ReadArray(() =>
        {
            var propertyTypeName = Ar.ReadFName();
            var prop = FField.Construct(propertyTypeName);
            prop.Deserialize(Ar);
            return prop;
        }) ?? Array.Empty<FField>();
    }

    public bool GetProperty(FName name, out FField? property)
    {
        property = null;
        foreach (var item in ChildProperties)
        {
            if (item.Name.Text == name.Text)
            {
                property = item;
                return true;
            }
        }
        return false;
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        if (!SuperStruct.IsNull && (!SuperStruct.ResolvedObject?.Equals(Super) ?? false))
        {
            writer.WritePropertyName("SuperStruct");
            serializer.Serialize(writer, SuperStruct);
        }

        if (Children.Length > 0)
        {
            writer.WritePropertyName("Children");
            serializer.Serialize(writer, Children);
        }

        if (ChildProperties.Length > 0)
        {
            writer.WritePropertyName("ChildProperties");
            serializer.Serialize(writer, ChildProperties);
        }

        if (ScriptBytecode.Length > 0)
        {
            writer.WritePropertyName("ScriptBytecode");
            writer.WriteStartArray();

            foreach (var expr in ScriptBytecode)
            {
                writer.WriteStartObject();
                expr.WriteJson(writer, serializer, true);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }
    }
}