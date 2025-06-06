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
            throw new ParserException(Ar, "�޷�����û�б�ǩ���ݵĿ�ѡ����");
        if (tagData.InnerType == null)
            throw new ParserException(Ar, "��ѡ������Ҫ�ڲ�����");

        if (type == ReadType.ZERO || !Ar.ReadBoolean())
        {
            Value = default;
            return;
        }

        Value = ReadPropertyTagType(Ar, tagData.InnerType, tagData.InnerTypeData, ReadType.OPTIONAL) ?? default;
    }
}