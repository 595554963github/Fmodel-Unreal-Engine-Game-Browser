using System.ComponentModel;

namespace CUE4Parse_Conversion.UEFormat.Enums;

public enum EFileCompressionFormat
{
    [Description("Î´Ñ¹ËõµÄ")]
    None,
    
    [Description("GzipÑ¹ËõµÄ")]
    GZIP,
    
    [Description("ZstdÑ¹ËõµÄ")]
    ZSTD
}