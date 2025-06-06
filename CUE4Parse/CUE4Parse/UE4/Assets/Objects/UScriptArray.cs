using System;
using System.Collections.Generic;
using CUE4Parse.GameTypes.DaysGone.Assets;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Assets.Objects;

[JsonConverter(typeof(UScriptArrayConverter))]
public class UScriptArray
{
    public readonly string InnerType;
    public readonly FPropertyTagData? InnerTagData;
    public readonly List<FPropertyTagType> Properties;

    public UScriptArray(string innerType)
    {
        InnerType = innerType;
        InnerTagData = null;
        Properties = [];
    }

    public UScriptArray(FAssetArchive Ar, FPropertyTagData? tagData, int size)
    {
        InnerType = tagData?.InnerType ?? throw new ParserException(Ar, "UScriptArray��Ҫ�ڲ�����");
        var elementCount = Ar.Read<int>();

        if (elementCount > Ar.Length - Ar.Position)
        {
            throw new ParserException(Ar,
                $"��������Ԫ������{elementCount}����ʣ��ĵ�����С {Ar.Length - Ar.Position}");
        }

        if (Ar.HasUnversionedProperties)
        {
            InnerTagData = tagData.InnerTypeData;
        }
        else if (Ar.Ver >= EUnrealEngineObjectUE5Version.PROPERTY_TAG_COMPLETE_TYPE_NAME && InnerType == "StructProperty")
        {
            InnerTagData = tagData.InnerTypeData;
        }
        else if (Ar.Ver >= EUnrealEngineObjectUE4Version.INNER_ARRAY_TAG_INFO && InnerType == "StructProperty")
        {
            InnerTagData = new FPropertyTag(Ar, false).TagData;
            if (InnerTagData == null)
                throw new ParserException(Ar, $"�޷���ȡ�ڲ�����Ϊ{InnerType}����������");
        }
        else
        {
            if (Ar.Game == EGame.GAME_���ղ��� && InnerType == "StructProperty")
            {
                var count = elementCount > 0 ? elementCount : 1;
                var elemsize = (size - sizeof(int)) / count;
                InnerTagData = DaysGoneProperties.GetArrayStructType(tagData.Name, elemsize);
            }
        }

        Properties = new List<FPropertyTagType>(elementCount);
        if (elementCount == 0) return;

        // �ֽ����Ե����⴦����Ϊ���ȿ�����Ϊ�����ֽڶ�ȡ��Ҳ������Ϊö�����Զ�ȡ
        if (InnerType == "ByteProperty")
        {
            var enumprop = (InnerTagData?.EnumName != null && !InnerTagData.EnumName.Equals("None", StringComparison.OrdinalIgnoreCase)) || Ar.TestReadFName();
            if (!Ar.HasUnversionedProperties) enumprop = (size - sizeof(int)) / elementCount > 1;
            for (var i = 0; i < elementCount; i++)
            {
                var property = enumprop ? (FPropertyTagType?)new EnumProperty(Ar, InnerTagData, ReadType.ARRAY) : new ByteProperty(Ar, ReadType.ARRAY);
                if (property != null)
                    Properties.Add(property);
                else
                    Log.Debug($"��λ��${Ar.Position}������{i}����ȡ����Ϊ{InnerType} ����������ʧ��");
            }
            return;
        }

        for (var i = 0; i < elementCount; i++)
        {
            var property = FPropertyTagType.ReadPropertyTagType(Ar, InnerType, InnerTagData, ReadType.ARRAY);
            if (property != null)
                Properties.Add(property);
            else
                Log.Debug($"��λ��${Ar.Position}������{i}����ȡ����Ϊ{InnerType} ����������ʧ��");
        }
    }

    public override string ToString() => $"{InnerType}[{Properties.Count}]";
}