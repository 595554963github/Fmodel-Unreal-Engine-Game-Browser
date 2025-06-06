using System.Collections.Generic;
using CUE4Parse.UE4.Objects.Core.Math;

namespace CUE4Parse_Conversion.Meshes.PSK
{
    public class CStaticMesh
    {
        public List<CStaticMeshLod> LODs;
        public FBox BoundingBox;
        public FSphere BoundingSphere;

        public CStaticMesh()
        {
            LODs = new List<CStaticMeshLod>();
            BoundingBox = new FBox();
            BoundingSphere = new FSphere(); 
        }

        public void FinalizeMesh()
        {
            foreach (var levelOfDetail in LODs)
            {
                levelOfDetail?.BuildNormals();
            }
        }
    }
}