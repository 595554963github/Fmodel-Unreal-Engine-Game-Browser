using System;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Assets.Exports.StaticMesh;

public class UStaticMesh : UObject
{
    public bool bCooked { get; private set; }
    public FPackageIndex BodySetup { get; private set; } = new();
    public FPackageIndex NavCollision { get; private set; } = new();
    public FGuid LightingGuid { get; private set; } = new(); 
    public FPackageIndex[] Sockets { get; private set; } = Array.Empty<FPackageIndex>();
    public FStaticMeshRenderData? RenderData { get; private set; } 
    public FStaticMaterial[]? StaticMaterials { get; private set; } 
    public ResolvedObject?[] Materials { get; private set; } = Array.Empty<ResolvedObject?>(); 
    public int LODForCollision { get; private set; }

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        Materials = Array.Empty<ResolvedObject?>();
        LODForCollision = GetOrDefault(nameof(LODForCollision), 0);

        var stripDataFlags = new FStripDataFlags(Ar);
        bCooked = Ar.ReadBoolean();
        if (Ar.Game == EGame.GAME_落日余晖) Ar.Position += 1;
        BodySetup = new FPackageIndex(Ar);

        if (Ar.Versions["StaticMesh.HasNavCollision"])
            NavCollision = new FPackageIndex(Ar);

        if (!stripDataFlags.IsEditorDataStripped())
        {
            Log.Warning("尚未实现包含编辑器数据的静态网格体");
            Ar.Position = validPos;
            return;
        }

        LightingGuid = Ar.Read<FGuid>();
        Sockets = Ar.ReadArray(() => new FPackageIndex(Ar)) ?? Array.Empty<FPackageIndex>();

        if (bCooked)
            RenderData = new FStaticMeshRenderData(Ar);

        if (bCooked && Ar.Game is >= EGame.GAME_UE4_20 and < EGame.GAME_UE5_0 && Ar.Game != EGame.GAME_元梦之星)
        {
            var bHasOccluderData = Ar.ReadBoolean();
            if (bHasOccluderData)
            {
                if (Ar.Game is EGame.GAME_界外狂潮 && Ar.ReadBoolean())
                {
                    Ar.SkipBulkArrayData();
                    Ar.SkipBulkArrayData();
                }
                else
                {
                    Ar.SkipFixedArray(12);
                    Ar.SkipFixedArray(2);
                }
            }
        }

        if (Ar.Game >= EGame.GAME_UE4_14)
        {
            var bHasSpeedTreeWind = Ar.ReadBoolean();
            if (bHasSpeedTreeWind)
            {
                Ar.Position = validPos;
            }

            if (FEditorObjectVersion.Get(Ar) >= FEditorObjectVersion.Type.RefactorMeshEditorMaterials)
            {
                StaticMaterials = bHasSpeedTreeWind
                    ? GetOrDefault("StaticMaterials", Array.Empty<FStaticMaterial>())
                    : Ar.ReadArray(() => new FStaticMaterial(Ar)) ?? Array.Empty<FStaticMaterial>();

                Materials = new ResolvedObject?[StaticMaterials.Length];
                for (var i = 0; i < Materials.Length; i++)
                {
                    Materials[i] = StaticMaterials[i].MaterialInterface;
                }
            }
        }
        else if (TryGetValue(out FPackageIndex[]? materials, "Materials"))
        {
            if (materials != null)
            {
                Materials = new ResolvedObject?[materials.Length];
                for (var i = 0; i < materials.Length; i++)
                {
                    Materials[i] = materials[i].ResolvedObject;
                }
            }
        }

        Ar.Position += Ar.Game switch
        {
            EGame.GAME_逃生试炼 => 1,
            EGame.GAME_落日余晖 or EGame.GAME_沙丘_觉醒 => 4,
            EGame.GAME_往日不再 => Ar.Read<int>() * 4,
            _ => 0
        };
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        writer.WritePropertyName("BodySetup");
        serializer.Serialize(writer, BodySetup);

        writer.WritePropertyName("NavCollision");
        serializer.Serialize(writer, NavCollision);

        writer.WritePropertyName("LightingGuid");
        serializer.Serialize(writer, LightingGuid);

        writer.WritePropertyName("RenderData");
        serializer.Serialize(writer, RenderData);
    }
}