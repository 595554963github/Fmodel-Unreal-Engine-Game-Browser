using System.ComponentModel;

namespace CUE4Parse.UE4.Assets.Exports.Texture
{
    public enum ETexturePlatform
    {
        [Description("�����/�ƶ���")]
        DesktopMobile,
        [Description("Xbox One/Series / Playstation 4/5")]
        XboxAndPlaystation,
        [Description("������Switch")]
        NintendoSwitch
    }
}