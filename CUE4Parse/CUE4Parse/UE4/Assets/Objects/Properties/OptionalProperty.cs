using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Objects.Properties;

[JsonConverter(typeof(OptionalPropertyConverter))]
public class OptionalProperty : FPropertyTagType<FPropertyTagType>
{
    public OptionalProperty(FAssetArchive Ar, FPropertyTagData? tagData, ReadType type)
    {
        if (tagData == null)
            throw new ParserException(Ar, "无法加载没有标签数据的可选属性");
        if (tagData.InnerType == null)
            throw new ParserException(Ar, "可选属性需要内部类型");

        if (type == ReadType.ZERO || !Ar.ReadBoolean())
        {
            Value = default;
            return;
        }

        Value = ReadPropertyTagType(Ar, tagData.InnerType, tagData.InnerTypeData, ReadType.OPTIONAL) ?? default;
    }
}