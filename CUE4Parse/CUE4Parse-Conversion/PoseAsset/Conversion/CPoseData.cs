using CUE4Parse_Conversion.PoseAsset.Conversion;
using System.Collections.Generic;

public class CPoseData
{
    public string PoseName;
    public List<CPoseKey> Keys = [];
    public float[] CurveData;

    public CPoseData(string poseName, float[] curveData)
    {
        PoseName = poseName;
        CurveData = curveData;
    }
}