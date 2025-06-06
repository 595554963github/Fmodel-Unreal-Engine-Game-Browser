using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Assets.Objects;

[JsonConverter(typeof(FInstancedStructConverter))]
public class FInstancedStruct : IUStruct
{
    public readonly FStructFallback? NonConstStruct;

    public FInstancedStruct(FAssetArchive Ar)
    {
        if (FInstancedStructCustomVersion.Get(Ar) < FInstancedStructCustomVersion.Type.CustomVersionAdded)
        {
            var headerOffset = Ar.Position;
            var header = Ar.Read<uint>();

            const uint LegacyEditorHeader = 0xABABABAB;
            if (header != LegacyEditorHeader)
            {
                Ar.Position = headerOffset;
            }

            _ = Ar.Read<byte>();
        }

        var strucindex = new FPackageIndex(Ar);
        var serialSize = Ar.Read<int>();
        var savedPos = Ar.Position;

        if (strucindex.IsNull)
        {
            Ar.Position = savedPos + serialSize;
            return;
        }

        try
        {
            if (strucindex.TryLoad<UStruct>(out var struc))
            {
                NonConstStruct = new FStructFallback(Ar, struc);
            }
            else if (strucindex.ResolvedObject is { } obj)
            {
                NonConstStruct = new FStructFallback(Ar, obj.Name.ToString());
            }
            else
            {
                Log.Warning("读取{0}类型的FInstancedstruct失败,跳过它", strucindex.ResolvedObject?.GetFullName());
            }
        }
        catch (ParserException e)
        {
            Log.Warning(e, "读取{0}类型的FInstancedstruct失败,跳过它", strucindex.ResolvedObject?.GetFullName());
        }
        finally
        {
            Ar.Position = savedPos + serialSize;
        }
    }
}