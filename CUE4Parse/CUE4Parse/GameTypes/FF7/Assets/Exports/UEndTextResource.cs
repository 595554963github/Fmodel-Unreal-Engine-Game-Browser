using System.Collections.Generic;
using CUE4Parse.GameTypes.FF7.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Exceptions;
using Newtonsoft.Json;

namespace CUE4Parse.GameTypes.FF7.Assets.Exports;

public class UEndTextResource : UObject
{
    public Dictionary<string, FEndTextResourceStrings>? Strings;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        var count = Ar.Read<int>();
        Strings = [];
        for (var i = 0; i < count; ++i)
        {
            var key = Ar.ReadFString();
            if (string.IsNullOrWhiteSpace(key) || key[0] != '$')
                throw new ParserException(Ar, $"结束文本资源'{Ar.Name}'不是以魔数符号开头的!");
            var resource = new FEndTextResourceStrings(Ar);
            Strings.Add(key, resource);
        }
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        if (Strings?.Count > 0)
        {
            writer.WritePropertyName("字符串");
            writer.WriteStartObject();
            foreach (var (key, val) in Strings)
            {
                writer.WritePropertyName(key);
                serializer.Serialize(writer, val);
            }
            writer.WriteEndObject();
        }
    }
}