using System.ComponentModel;

namespace CUE4Parse_Conversion.Meshes
{
    public enum EMeshFormat
    {
        [Description("ActorX(psk/pskx)")]
        ActorX,
        [Description("glTF 2.0(binary)")]
        Gltf2,
        [Description("Wavefront OBJ(未实施)")]
        OBJ,
        [Description("UEFormat(uemodel)")]
        UEFormat
    }
}