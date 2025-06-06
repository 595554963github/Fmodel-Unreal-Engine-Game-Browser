using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Localization;

[JsonConverter(typeof(FTextLocalizationResourceConverter))]
public class FTextLocalizationResource
{
    private readonly FGuid _locResMagic = new(0x7574140Eu, 0xFC034A67u, 0x9D90154Au, 0x1B7F37C3u);
    public readonly Dictionary<FTextKey, Dictionary<FTextKey, FEntry>> Entries = [];

    public FTextLocalizationResource(FArchive Ar)
    {
        var locResMagic = Ar.Read<FGuid>();
        var versionNumber = ELocResVersion.Legacy;
        if (locResMagic == _locResMagic)
        {
            versionNumber = Ar.Read<ELocResVersion>();
        }
        else // Legacy LocRes files lack the magic number
        {
            Ar.Position = 0;
            Log.Warning($"本地化资源文件{Ar.Name}'魔数校验失败！假设这是一个旧版资源");
        }

        // Check if version is too new
        if ((int)versionNumber > (int)ELocResVersion.Latest)
        {
            if (Ar.Game != EGame.GAME_剑星)
                throw new ParserException($"本地化资源文件'{Ar.Name}'版本过高，无法加载(文件版本:{versionNumber:D},加载器版本:{ELocResVersion.Latest:D})");
        }

        // Read localized string array
        var localizedStringArray = Array.Empty<FTextLocalizationResourceString>();
        if (versionNumber >= ELocResVersion.Compact)
        {
            var localizedStringArrayOffset = Ar.Read<long>();
            if (localizedStringArrayOffset != -1) // INDEX_NONE
            {
                var currentFileOffset = Ar.Position;
                Ar.Position = localizedStringArrayOffset;
                localizedStringArray = Ar.ReadArray(() => new FTextLocalizationResourceString(Ar, versionNumber));
                Ar.Position = currentFileOffset;
            }
        }

        // Read entries count
        if (versionNumber >= ELocResVersion.Optimized_CRC32)
        {
            Ar.Position += 4; // Skip EntriesCount
        }

        // Read namespace count
        var namespaceCount = Ar.Read<uint>();
        for (var i = 0; i < namespaceCount; i++)
        {
            var namespce = new FTextKey(Ar, versionNumber);
            var keyCount = Ar.Read<uint>();
            var keyValue = new Dictionary<FTextKey, FEntry>((int)keyCount);

            for (var j = 0; j < keyCount; j++)
            {
                var key = new FTextKey(Ar, versionNumber);
                FEntry newEntry = new(Ar);

                if (versionNumber >= ELocResVersion.Compact)
                {
                    var localizedStringIndex = Ar.Read<int>();
                    if (localizedStringIndex >= 0 && localizedStringIndex < localizedStringArray.Length)
                    {
                        var localizedString = localizedStringArray[localizedStringIndex];
                        newEntry.LocalizedString = localizedString.String;
                        if (localizedString.RefCount != -1)
                            localizedString.RefCount--;
                    }
                    else
                    {
                        Log.Warning($"本地化资源文件'{newEntry.LocResName}'中命名空间'{namespce.Str}'和键'{key.Str}'的本地化字符串索引无效");
                    }

                    if (Ar.Game == EGame.GAME_剑星 && versionNumber > ELocResVersion.Latest)
                        Ar.Position += 4;
                }
                else
                {
                    newEntry.LocalizedString = Ar.ReadFString();
                }

                keyValue.Add(key, newEntry);
            }
            Entries.Add(namespce, keyValue);
        }
    }
}

// Add this struct definition to make FTextLocalizationResourceString a value type
public struct FTextLocalizationResourceString
{
    public string String;
    public int RefCount;

    public FTextLocalizationResourceString(FArchive Ar, ELocResVersion versionNumber)
    {
        String = Ar.ReadFString();
        RefCount = versionNumber >= ELocResVersion.Compact ? Ar.Read<int>() : -1;
    }
}