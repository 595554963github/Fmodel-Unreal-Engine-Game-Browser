using System.ComponentModel;

namespace CUE4Parse.UE4.Assets.Exports.Material
{
    public enum EMaterialFormat
    {
        [Description("仅限第一层")]
        FirstLayer,
        [Description("所有层(没有引用纹理)")]
        AllLayersNoRef,
        [Description("所有层(与所有引用的纹理)")]
        AllLayers
    }
}