using System;
using System.ComponentModel;

namespace FModel;

public enum EBuildKind
{
    Debug,
    Release,
    Unknown
}

public enum EErrorKind
{
    Ignore,
    Restart,
    ResetSettings
}
public enum SettingsOut
{
    ReloadLocres,
    ReloadMappings
}

public enum EStatusKind
{
    就绪, // ready
    加载中, // doing stuff
    正在停止, // trying to stop
    已停止, // stopped
    失败, // crashed
    已完成 // worked
}
public enum EAesReload
{
    [Description("总是")]
    Always,
    [Description("从不")]
    Never,
    [Description("每天一次")]
    OncePerDay
}

public enum EDiscordRpc
{
    [Description("总是")]
    Always,
    [Description("从不")]
    Never
}

public enum ELoadingMode
{
    [Description("复选")]
    Multiple,
    [Description("所有")]
    All,
    [Description("所有(新的)")]
    AllButNew,
    [Description("所有(修改的)")]
    AllButModified
}

// public enum EUpdateMode
// {
//     [Description("Stable")]
//     Stable,
//     [Description("Beta")]
//     Beta,
//     [Description("QA Testing")]
//     Qa
// }

public enum ECompressedAudio
{
    [Description("播放解压的数据")]
    PlayDecompressed,
    [Description("播放压缩数据(可能并不总是有效的音频数据)")]
    PlayCompressed
}

public enum EIconStyle
{
    [Description("默认")]
    Default,
    [Description("无背景")]
    NoBackground,
    [Description("无文本")]
    NoText,
    [Description("扁平")]
    Flat,
    [Description("卡塔巴")]
    Cataba,
    // [Description("社区")]
    // CommunityMade
}

public enum EEndpointType
{
    Aes,
    Mapping
}

[Flags]
public enum EBulkType
{
    None =          0,
    Auto =          1 << 0,
    Properties =    1 << 1,
    Textures =      1 << 2,
    Meshes =        1 << 3,
    Skeletons =     1 << 4,
    Animations =    1 << 5
}