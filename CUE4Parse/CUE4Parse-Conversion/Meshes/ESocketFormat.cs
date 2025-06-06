using System.ComponentModel;

namespace CUE4Parse_Conversion.Meshes;

public enum ESocketFormat
{
    [Description("在单独的标头中导出骨插座(SKELSOCK)")]
    Socket,
    [Description("将骨插座导出为骨骼")]
    Bone,
    [Description("不导出骨插座")]
    None
}