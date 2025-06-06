using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Assets.Objects;

[JsonConverter(typeof(UScriptSetConverter))]
public class UScriptSet
{
    public readonly List<FPropertyTagType> Properties;

    public UScriptSet()
    {
        Properties = new List<FPropertyTagType>();
    }

    public UScriptSet(FAssetArchive Ar, FPropertyTagData? tagData)
    {
        if (Ar.Game == EGame.GAME_���ù���2 && tagData != null)
        {
            tagData.InnerType = tagData.Name switch
            {
                "AllEntityIds" or "SceneNameSet" => "NameProperty",
                "TextVarSources" => "StrProperty",
                _ => null
            };
        }

        var innerType = tagData?.InnerType ?? throw new ParserException(Ar, "UScriptSet��Ҫ�ڲ�����");

        if (tagData != null && tagData.InnerTypeData is null && !Ar.HasUnversionedProperties && innerType == "StructProperty")
        {
            if (tagData.Name is "AnimSequenceInstances" or "PostProcessInstances")
            {
                tagData.InnerTypeData = new FPropertyTagData("Guid");
            }
            if (Ar.Game == EGame.GAME_��Ȩ������ && tagData.Name is "ExcludeMeshes" or "IncludeMeshes")
            {
                tagData.InnerTypeData = new FPropertyTagData("SoftObjectPath");
            }
            if (Ar.Game == EGame.GAME_�������� && tagData.Name is "SoundscapePaletteCollection")
            {
                tagData.InnerTypeData = new FPropertyTagData("SoftObjectPath");
            }
            if (Ar.Game == EGame.GAME_���� && tagData.Name?.EndsWith("IDs") == true)
            {
                tagData.InnerTypeData = new FPropertyTagData("Guid");
            }
        }

        var numElementsToRemove = Ar.Read<int>();
        for (var i = 0; i < numElementsToRemove; i++)
        {
            FPropertyTagType.ReadPropertyTagType(Ar, innerType, tagData?.InnerTypeData, ReadType.ARRAY);
        }

        var num = Ar.Read<int>();
        Properties = new List<FPropertyTagType>(num);
        for (var i = 0; i < num; i++)
        {
            var property = FPropertyTagType.ReadPropertyTagType(Ar, innerType, tagData?.InnerTypeData, ReadType.ARRAY);
            if (property != null)
                Properties.Add(property);
            else
                Log.Debug($"��ȡ����������{i}��Ԫ��ʧ��");
        }
    }
}