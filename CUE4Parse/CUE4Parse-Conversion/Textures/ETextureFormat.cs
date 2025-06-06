using System.ComponentModel;

namespace CUE4Parse_Conversion.Textures
{
    public enum ETextureFormat
    {
        [Description("PNG")]
        Png,
        [Description("JPEG")]
        Jpeg,
        [Description("TGA")]
        Tga,
        [Description("DDS(未实施)")]
        Dds
    }
}