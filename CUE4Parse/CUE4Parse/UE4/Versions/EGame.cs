using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Versions;

[JsonConverter(typeof(EGameConverter))]
public enum EGame : uint
{
    // bytes: 04.NN.FF.XX : 04/05=UE4/5, NN=UE4 subversion, FF=Flags (curently not used), XX=game (0=base engine)
    GAME_UE4_0 = GameUtils.GameUe4Base + (0 << 16),
    GAME_UE4_1 = GameUtils.GameUe4Base + (1 << 16),
    GAME_UE4_2 = GameUtils.GameUe4Base + (2 << 16),
    GAME_UE4_3 = GameUtils.GameUe4Base + (3 << 16),
    GAME_UE4_4 = GameUtils.GameUe4Base + (4 << 16),
    GAME_UE4_5 = GameUtils.GameUe4Base + (5 << 16),
        GAME_方舟_生存进化 = GAME_UE4_5 + 1,
    GAME_UE4_6 = GameUtils.GameUe4Base + (6 << 16),
    GAME_UE4_7 = GameUtils.GameUe4Base + (7 << 16),
    GAME_UE4_8 = GameUtils.GameUe4Base + (8 << 16),
    GAME_UE4_9 = GameUtils.GameUe4Base + (9 << 16),
    GAME_UE4_10 = GameUtils.GameUe4Base + (10 << 16),
        GAME_盗贼之海 = GAME_UE4_10 + 1,
    GAME_UE4_11 = GameUtils.GameUe4Base + (11 << 16),
        GAME_战争机器4 = GAME_UE4_11 + 1,
        GAME_往日不再 = GAME_UE4_11 + 2,
    GAME_UE4_12 = GameUtils.GameUe4Base + (12 << 16),
    GAME_UE4_13 = GameUtils.GameUe4Base + (13 << 16),
        GAME_腐烂国度2 = GAME_UE4_13 + 1,
    GAME_UE4_14 = GameUtils.GameUe4Base + (14 << 16),
        GAME_铁拳7 = GAME_UE4_14 + 1,
    GAME_UE4_15 = GameUtils.GameUe4Base + (15 << 16),
    GAME_UE4_16 = GameUtils.GameUe4Base + (16 << 16),
        GAME_绝地求生大逃杀 = GAME_UE4_16 + 1,
        GAME_模拟火车世界2020 = GAME_UE4_16 + 2,
    GAME_UE4_17 = GameUtils.GameUe4Base + (17 << 16),
        GAME_逃出生天 = GAME_UE4_17 + 1,
    GAME_UE4_18 = GameUtils.GameUe4Base + (18 << 16),
        GAME_王国之心3 = GAME_UE4_18 + 1,
        GAME_最终幻想7重制版 = GAME_UE4_18 + 2,
        GAME_皇牌空战7 = GAME_UE4_18 + 3,
        GAME_十三号星期五 = GAME_UE4_18 + 4,
        GAME_和平精英 = GAME_UE4_18 + 5,
    GAME_UE4_19 = GameUtils.GameUe4Base + (19 << 16),
        GAME_虚幻争霸 = GAME_UE4_19 + 1,
    GAME_UE4_20 = GameUtils.GameUe4Base + (20 << 16),
        GAME_无主之地3 = GAME_UE4_20 + 1,
    GAME_UE4_21 = GameUtils.GameUe4Base + (21 << 16),
        GAME_星球大战绝地_陨落的武士团 = GAME_UE4_21 + 1,
        GAME_黎明觉醒 = GAME_UE4_21 + 2,
    GAME_UE4_22 = GameUtils.GameUe4Base + (22 << 16),
    GAME_UE4_23 = GameUtils.GameUe4Base + (23 << 16),
        GAME_Apex英雄移动版 = GAME_UE4_23 + 1,
    GAME_UE4_24 = GameUtils.GameUe4Base + (24 << 16),
        GAME_托尼霍克职业滑板12 = GAME_UE4_24 + 1,
        GAME_大隆隆声拳击_信条冠军 = GAME_UE4_24 + 2,
    GAME_UE4_25 = GameUtils.GameUe4Base + (25 << 16),
        GAME_UE4_25_Plus = GAME_UE4_25 + 1,
        GAME_侠盗公司 = GAME_UE4_25 + 2,
        GAME_死亡岛2 = GAME_UE4_25 + 3,
        GAME_柯娜精神之桥 = GAME_UE4_25 + 4,
        GAME_卡拉彼丘 = GAME_UE4_25 + 5,
        GAME_重生边缘 = GAME_UE4_25 + 6,
        GAME_天启行动 = GAME_UE4_25 + 7,
        GAME_落日余晖 = GAME_UE4_25 + 8,
        GAME_星球大战银河猎手 = GAME_UE4_25 + 9,
        GAME_无路之旅 = GAME_UE4_25 + 10,
    GAME_UE4_26 = GameUtils.GameUe4Base + (26 << 16),
        GAME_侠盗猎车手_三部曲_终级版 = GAME_UE4_26 + 1,
        GAME_严阵以待 = GAME_UE4_26 + 2,
        GAME_剑灵2 = GAME_UE4_26 + 3,
        GAME_幻塔 = GAME_UE4_26 + 4,
        GAME_最终幻想7重生 = GAME_UE4_26 + 5,
        GAME_全境封锁_曙光 = GAME_UE4_26 + 6,
        GAME_星球大战绝地_幸存者 = GAME_UE4_26 + 7,
        GAME_尘白禁区 = GAME_UE4_26 + 8,
        GAME_火炬之光_无限 = GAME_UE4_26 + 9,
        GAME_QQ_这tm算游戏 = GAME_UE4_26 + 10,
        GAME_鸣潮 = GAME_UE4_26 + 11,
        GAME_元梦之星 = GAME_UE4_26 + 12,
        GAME_午夜太阳 = GAME_UE4_26 + 13,
        GAME_界外狂潮 = GAME_UE4_26 + 14,
        GAME_巅峰极速 = GAME_UE4_26 + 15,
        GAME_剑星 = GAME_UE4_26 + 16,
    GAME_UE4_27 = GameUtils.GameUe4Base + (27 << 16),
        GAME_分裂之门 = GAME_UE4_27 + 1,
        GAME_HYENAS_直译为鬣狗 = GAME_UE4_27 + 2,
        GAME_霍格沃茨遗产 = GAME_UE4_27 + 3,
        GAME_逃生试炼 = GAME_UE4_27 + 4,
        GAME_无畏契约 = GAME_UE4_27 + 5,
        GAME_指环王_中土之战 = GAME_UE4_27 + 6,
        GAME_禁闭求生 = GAME_UE4_27 + 7,
        GAME_三角洲行动 = GAME_UE4_27 + 8,
       GAME_真人快打1 = GAME_UE4_27 + 9,
        GAME_圣剑传说 = GAME_UE4_27 + 10,
        GAME_幽灵分类 = GAME_UE4_27 + 11,
        GAME_跑跑卡丁车_漂移 = GAME_UE4_27 + 12,
        GAME_王权与自由 = GAME_UE4_27 + 13,
        GAME_世界摩托大奖赛24 = GAME_UE4_27 + 14,
        GAME_迷失 = GAME_UE4_27 + 15,
        GAME_晶核 = GAME_UE4_27 + 16,
        GAME_达愿福神社 = GAME_UE4_27 + 17,
    GAME_UE4_28 = GameUtils.GameUe4Base + (28 << 16),

    GAME_UE4_LATEST = GAME_UE4_28,

    // TODO Figure out the enum name for UE5 Early Access
    // The commit https://github.com/EpicGames/UnrealEngine/commit/cf116088ae6b65c1701eee99288e43c7310d6bb1#diff-6178e9d97c98e321fc3f53770109ea7f6a8ea7a86cac542717a81922f2f93613R723
    // changed the IoStore and its packages format which breaks backward compatibility with 5.0.0-16433597+++UE5+Release-5.0-EarlyAccess
    GAME_UE5_0 = GameUtils.GameUe5Base + (0 << 16),
        GAME_遇见造物主 = GAME_UE5_0 + 1,
        GAME_黑神话_悟空 = GAME_UE5_0 + 2,
    GAME_UE5_1 = GameUtils.GameUe5Base + (1 << 16),
        GAME_街头篮球_反弹 = GAME_UE5_1 + 1,
        GAME_潜行者2_切尔诺贝利之心 = GAME_UE5_1 + 2,
        GAME_弗兰克斯通的阴影 = GAME_UE5_1 + 3,
        GAME_寂静岭2重制版 = GAME_UE5_1 + 4,
    GAME_UE5_2 = GameUtils.GameUe5Base + (2 << 16),
        GAME_黎明杀机 = GAME_UE5_2 + 1,
        GAME_天域安宁 = GAME_UE5_2 + 2,
        GAME_第一后裔 = GAME_UE5_2 + 3,
        GAME_地铁觉醒 = GAME_UE5_2 + 4,
        GAME_方舟_生存升级 = GAME_UE5_2 + 5,
        GAME_沙丘_觉醒 = GAME_UE5_2 + 6,
    GAME_UE5_3 = GameUtils.GameUe5Base + (3 << 16),
        GAME_漫威争锋 = GAME_UE5_3 + 1,
        GAME_Placeholder = GAME_UE5_3 + 2, // Placeholder for a game that hasn't been added yet
        GAME_无人愿死 = GAME_UE5_3 + 3, // no use
        GAME_怪物卡车对决 = GAME_UE5_3 + 4,
        GAME_Rennsport = GAME_UE5_3 + 5,
        GAME_创世之烬 = GAME_UE5_3 + 6,
        GAME_宣誓 = GAME_UE5_3 + 7,
    GAME_UE5_4 = GameUtils.GameUe5Base + (4 << 16),
        GAME_公仔总动员 = GAME_UE5_4 + 1,
        GAME_无限暖暖= GAME_UE5_4 + 2,
        GAME_异环 = GAME_UE5_4 + 3,
        GAME_哥特王朝重制版 = GAME_UE5_4 + 4,
        GAME_双影奇境 = GAME_UE5_4 + 5,
        GAME_兽猎突袭 = GAME_UE5_4 + 6,
        GAME_云族裔 = GAME_UE5_4 + 7,
        GAME_风暴崛起 = GAME_UE5_4 + 8,
    GAME_UE5_5 = GameUtils.GameUe5Base + (5 << 16),
        GAME_Brickadia = GAME_UE5_5 + 1,
        GAME_分裂之门2 = GAME_UE5_5 + 2,
        GAME_死域Rogue = GAME_UE5_5 + 3,
        GAME_摩托GP25 = GAME_UE5_5 + 4,
        GAME_无主星渊 = GAME_UE5_5 + 5,
    GAME_UE5_6 = GameUtils.GameUe5Base + (6 << 16),
    GAME_UE5_7 = GameUtils.GameUe5Base + (7 << 16),

    GAME_UE5_LATEST = GAME_UE5_6
}

public static class GameUtils
{
    public const int GameUe4Base = 0x4000000;
    public const int GameUe5Base = 0x5000000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GAME_UE4(int x)
    {
        return GameUe4Base + (x << 16);
    }

    public static FPackageFileVersion GetVersion(this EGame game)
    {
        // Custom UE Games
        // If a game needs a even more specific custom version than the major release version you can add it below
        // if (game == EGame.GAME_VALORANT)
        //     return UE4Version.VER_UE4_24;

        if (game >= EGame.GAME_UE5_0)
        {
            return game switch
            {
                < EGame.GAME_UE5_1 => new FPackageFileVersion(522, 1004),
                < EGame.GAME_UE5_2 => new FPackageFileVersion(522, 1008),
                    EGame.GAME_第一后裔 => new FPackageFileVersion(522, 1002),
                < EGame.GAME_UE5_4 => new FPackageFileVersion(522, 1009),
                < EGame.GAME_UE5_5 => new FPackageFileVersion(522, 1012),
                < EGame.GAME_UE5_6 => new FPackageFileVersion(522, 1013),
                _ => new FPackageFileVersion((int) EUnrealEngineObjectUE4Version.AUTOMATIC_VERSION, (int) EUnrealEngineObjectUE5Version.AUTOMATIC_VERSION)
            };
        }

        return FPackageFileVersion.CreateUE4Version(game switch
        {
            // General UE4 Versions
            < EGame.GAME_UE4_1 => 342,
            < EGame.GAME_UE4_2 => 352,
            < EGame.GAME_UE4_3 => 363,
            < EGame.GAME_UE4_4 => 382,
            < EGame.GAME_UE4_5 => 385,
            < EGame.GAME_UE4_6 => 401,
            < EGame.GAME_UE4_7 => 413,
            < EGame.GAME_UE4_8 => 434,
            < EGame.GAME_UE4_9 => 451,
            < EGame.GAME_UE4_10 => 482,
            < EGame.GAME_UE4_11 => 482,
            < EGame.GAME_UE4_12 => 498,
            < EGame.GAME_UE4_13 => 504,
            < EGame.GAME_UE4_14 => 505,
            < EGame.GAME_UE4_15 => 508,
            < EGame.GAME_UE4_16 => 510,
            < EGame.GAME_UE4_17 => 513,
            < EGame.GAME_UE4_18 => 513,
            < EGame.GAME_UE4_19 => 514,
            < EGame.GAME_UE4_20 => 516,
            < EGame.GAME_UE4_21 => 516,
            < EGame.GAME_UE4_22 => 517,
            < EGame.GAME_UE4_23 => 517,
            < EGame.GAME_UE4_24 => 517,
            < EGame.GAME_UE4_25 => 518,
            < EGame.GAME_UE4_26 => 518,
            < EGame.GAME_UE4_27 => 522,
            _ => (int) EUnrealEngineObjectUE4Version.AUTOMATIC_VERSION
        });
    }
}

public class EGameConverter : JsonConverter<EGame>
{
    public override void WriteJson(JsonWriter writer, EGame value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }

    public override EGame ReadJson(JsonReader reader, Type objectType, EGame existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Integer)
        {
            uint value = Convert.ToUInt32(reader.Value);
            return value > 0xFFFFFFF ? (EGame) ((value >> 28) + 3 << 24 | ((value >> 4) & 0xFF) << 16 | value & 0xF) : (EGame) value;
        }
        else if (reader is { TokenType: JsonToken.String, Value: string str })
        {
            return Enum.Parse<EGame>(str);
        }

        return EGame.GAME_UE4_LATEST;
    }
}