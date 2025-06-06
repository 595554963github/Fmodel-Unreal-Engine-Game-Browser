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
        GAME_����_������� = GAME_UE4_5 + 1,
    GAME_UE4_6 = GameUtils.GameUe4Base + (6 << 16),
    GAME_UE4_7 = GameUtils.GameUe4Base + (7 << 16),
    GAME_UE4_8 = GameUtils.GameUe4Base + (8 << 16),
    GAME_UE4_9 = GameUtils.GameUe4Base + (9 << 16),
    GAME_UE4_10 = GameUtils.GameUe4Base + (10 << 16),
        GAME_����֮�� = GAME_UE4_10 + 1,
    GAME_UE4_11 = GameUtils.GameUe4Base + (11 << 16),
        GAME_ս������4 = GAME_UE4_11 + 1,
        GAME_���ղ��� = GAME_UE4_11 + 2,
    GAME_UE4_12 = GameUtils.GameUe4Base + (12 << 16),
    GAME_UE4_13 = GameUtils.GameUe4Base + (13 << 16),
        GAME_���ù���2 = GAME_UE4_13 + 1,
    GAME_UE4_14 = GameUtils.GameUe4Base + (14 << 16),
        GAME_��ȭ7 = GAME_UE4_14 + 1,
    GAME_UE4_15 = GameUtils.GameUe4Base + (15 << 16),
    GAME_UE4_16 = GameUtils.GameUe4Base + (16 << 16),
        GAME_������������ɱ = GAME_UE4_16 + 1,
        GAME_ģ�������2020 = GAME_UE4_16 + 2,
    GAME_UE4_17 = GameUtils.GameUe4Base + (17 << 16),
        GAME_�ӳ����� = GAME_UE4_17 + 1,
    GAME_UE4_18 = GameUtils.GameUe4Base + (18 << 16),
        GAME_����֮��3 = GAME_UE4_18 + 1,
        GAME_���ջ���7���ư� = GAME_UE4_18 + 2,
        GAME_���ƿ�ս7 = GAME_UE4_18 + 3,
        GAME_ʮ���������� = GAME_UE4_18 + 4,
        GAME_��ƽ��Ӣ = GAME_UE4_18 + 5,
    GAME_UE4_19 = GameUtils.GameUe4Base + (19 << 16),
        GAME_������� = GAME_UE4_19 + 1,
    GAME_UE4_20 = GameUtils.GameUe4Base + (20 << 16),
        GAME_����֮��3 = GAME_UE4_20 + 1,
    GAME_UE4_21 = GameUtils.GameUe4Base + (21 << 16),
        GAME_�����ս����_�������ʿ�� = GAME_UE4_21 + 1,
        GAME_�������� = GAME_UE4_21 + 2,
    GAME_UE4_22 = GameUtils.GameUe4Base + (22 << 16),
    GAME_UE4_23 = GameUtils.GameUe4Base + (23 << 16),
        GAME_ApexӢ���ƶ��� = GAME_UE4_23 + 1,
    GAME_UE4_24 = GameUtils.GameUe4Base + (24 << 16),
        GAME_�������ְҵ����12 = GAME_UE4_24 + 1,
        GAME_��¡¡��ȭ��_�����ھ� = GAME_UE4_24 + 2,
    GAME_UE4_25 = GameUtils.GameUe4Base + (25 << 16),
        GAME_UE4_25_Plus = GAME_UE4_25 + 1,
        GAME_������˾ = GAME_UE4_25 + 2,
        GAME_������2 = GAME_UE4_25 + 3,
        GAME_���Ⱦ���֮�� = GAME_UE4_25 + 4,
        GAME_�������� = GAME_UE4_25 + 5,
        GAME_������Ե = GAME_UE4_25 + 6,
        GAME_�����ж� = GAME_UE4_25 + 7,
        GAME_�������� = GAME_UE4_25 + 8,
        GAME_�����ս�������� = GAME_UE4_25 + 9,
        GAME_��·֮�� = GAME_UE4_25 + 10,
    GAME_UE4_26 = GameUtils.GameUe4Base + (26 << 16),
        GAME_�����Գ���_������_�ռ��� = GAME_UE4_26 + 1,
        GAME_�����Դ� = GAME_UE4_26 + 2,
        GAME_����2 = GAME_UE4_26 + 3,
        GAME_���� = GAME_UE4_26 + 4,
        GAME_���ջ���7���� = GAME_UE4_26 + 5,
        GAME_ȫ������_��� = GAME_UE4_26 + 6,
        GAME_�����ս����_�Ҵ��� = GAME_UE4_26 + 7,
        GAME_���׽��� = GAME_UE4_26 + 8,
        GAME_���֮��_���� = GAME_UE4_26 + 9,
        GAME_QQ_��tm����Ϸ = GAME_UE4_26 + 10,
        GAME_���� = GAME_UE4_26 + 11,
        GAME_Ԫ��֮�� = GAME_UE4_26 + 12,
        GAME_��ҹ̫�� = GAME_UE4_26 + 13,
        GAME_����� = GAME_UE4_26 + 14,
        GAME_�۷弫�� = GAME_UE4_26 + 15,
        GAME_���� = GAME_UE4_26 + 16,
    GAME_UE4_27 = GameUtils.GameUe4Base + (27 << 16),
        GAME_����֮�� = GAME_UE4_27 + 1,
        GAME_HYENAS_ֱ��Ϊ�๷ = GAME_UE4_27 + 2,
        GAME_�����ִ��Ų� = GAME_UE4_27 + 3,
        GAME_�������� = GAME_UE4_27 + 4,
        GAME_��η��Լ = GAME_UE4_27 + 5,
        GAME_ָ����_����֮ս = GAME_UE4_27 + 6,
        GAME_�������� = GAME_UE4_27 + 7,
        GAME_�������ж� = GAME_UE4_27 + 8,
       GAME_���˿��1 = GAME_UE4_27 + 9,
        GAME_ʥ����˵ = GAME_UE4_27 + 10,
        GAME_������� = GAME_UE4_27 + 11,
        GAME_���ܿ�����_Ư�� = GAME_UE4_27 + 12,
        GAME_��Ȩ������ = GAME_UE4_27 + 13,
        GAME_����Ħ�д���24 = GAME_UE4_27 + 14,
        GAME_��ʧ = GAME_UE4_27 + 15,
        GAME_���� = GAME_UE4_27 + 16,
        GAME_��Ը������ = GAME_UE4_27 + 17,
    GAME_UE4_28 = GameUtils.GameUe4Base + (28 << 16),

    GAME_UE4_LATEST = GAME_UE4_28,

    // TODO Figure out the enum name for UE5 Early Access
    // The commit https://github.com/EpicGames/UnrealEngine/commit/cf116088ae6b65c1701eee99288e43c7310d6bb1#diff-6178e9d97c98e321fc3f53770109ea7f6a8ea7a86cac542717a81922f2f93613R723
    // changed the IoStore and its packages format which breaks backward compatibility with 5.0.0-16433597+++UE5+Release-5.0-EarlyAccess
    GAME_UE5_0 = GameUtils.GameUe5Base + (0 << 16),
        GAME_���������� = GAME_UE5_0 + 1,
        GAME_����_��� = GAME_UE5_0 + 2,
    GAME_UE5_1 = GameUtils.GameUe5Base + (1 << 16),
        GAME_��ͷ����_���� = GAME_UE5_1 + 1,
        GAME_Ǳ����2_�ж�ŵ����֮�� = GAME_UE5_1 + 2,
        GAME_������˹ͨ����Ӱ = GAME_UE5_1 + 3,
        GAME_�ž���2���ư� = GAME_UE5_1 + 4,
    GAME_UE5_2 = GameUtils.GameUe5Base + (2 << 16),
        GAME_����ɱ�� = GAME_UE5_2 + 1,
        GAME_������ = GAME_UE5_2 + 2,
        GAME_��һ���� = GAME_UE5_2 + 3,
        GAME_�������� = GAME_UE5_2 + 4,
        GAME_����_�������� = GAME_UE5_2 + 5,
        GAME_ɳ��_���� = GAME_UE5_2 + 6,
    GAME_UE5_3 = GameUtils.GameUe5Base + (3 << 16),
        GAME_�������� = GAME_UE5_3 + 1,
        GAME_Placeholder = GAME_UE5_3 + 2, // Placeholder for a game that hasn't been added yet
        GAME_����Ը�� = GAME_UE5_3 + 3, // no use
        GAME_���￨���Ծ� = GAME_UE5_3 + 4,
        GAME_Rennsport = GAME_UE5_3 + 5,
        GAME_����֮�� = GAME_UE5_3 + 6,
        GAME_���� = GAME_UE5_3 + 7,
    GAME_UE5_4 = GameUtils.GameUe5Base + (4 << 16),
        GAME_�����ܶ�Ա = GAME_UE5_4 + 1,
        GAME_����ůů= GAME_UE5_4 + 2,
        GAME_�컷 = GAME_UE5_4 + 3,
        GAME_�����������ư� = GAME_UE5_4 + 4,
        GAME_˫Ӱ�澳 = GAME_UE5_4 + 5,
        GAME_����ͻϮ = GAME_UE5_4 + 6,
        GAME_������ = GAME_UE5_4 + 7,
        GAME_�籩���� = GAME_UE5_4 + 8,
    GAME_UE5_5 = GameUtils.GameUe5Base + (5 << 16),
        GAME_Brickadia = GAME_UE5_5 + 1,
        GAME_����֮��2 = GAME_UE5_5 + 2,
        GAME_����Rogue = GAME_UE5_5 + 3,
        GAME_Ħ��GP25 = GAME_UE5_5 + 4,
        GAME_������Ԩ = GAME_UE5_5 + 5,
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
                    EGame.GAME_��һ���� => new FPackageFileVersion(522, 1002),
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