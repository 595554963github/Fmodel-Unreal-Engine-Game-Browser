using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Plugins.ScriptPlugin;

public class UScriptBlueprint : UBlueprint;

public class UScriptBlueprintGeneratedClass : UBlueprintGeneratedClass
{
    public List<FFieldPath> ScriptProperties = new();
    private UProperty[] ScriptProperties_DEPRECATED = Array.Empty<UProperty>(); 

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        ScriptProperties_DEPRECATED = GetOrDefault(nameof(ScriptProperties_DEPRECATED), Array.Empty<UProperty>());

        if (FCoreObjectVersion.Get(Ar) >= FCoreObjectVersion.Type.FProperties)
        {
            ScriptProperties = new List<FFieldPath>(Ar.ReadArray(() => new FFieldPath(Ar)));
        }
        else
        {
            foreach (var property in ScriptProperties_DEPRECATED)
            {
                // TODO: Implicit conversion
                // ScriptProperties.Add(property);
            }
        }
    }
}