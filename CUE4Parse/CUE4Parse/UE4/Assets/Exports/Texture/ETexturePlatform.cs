using System.ComponentModel;

namespace CUE4Parse.UE4.Assets.Exports.Texture
{
    public enum ETexturePlatform
    {
        [Description("桌面版/移动版")]
        DesktopMobile,
        [Description("Xbox One/Series / Playstation 4/5")]
        XboxAndPlaystation,
        [Description("任天堂Switch")]
        NintendoSwitch
    }
}