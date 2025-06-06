using System.ComponentModel;

namespace CUE4Parse_Conversion.UEFormat.Enums;

public enum EFileCompressionFormat
{
    [Description("δѹ����")]
    None,
    
    [Description("Gzipѹ����")]
    GZIP,
    
    [Description("Zstdѹ����")]
    ZSTD
}