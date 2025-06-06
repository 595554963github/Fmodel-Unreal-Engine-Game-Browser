using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Objects.RigVM
{
    public class FRigVMParameter
    {
        public ERigVMParameterType Type;
        public FName Name;
        public int RegisterIndex;
        public string CPPType = string.Empty;
        public FPackageIndex? ScriptStruct;
        public FName ScriptStructPath;

        public FRigVMParameter(FAssetArchive Ar)
        {
            if (FAnimObjectVersion.Get(Ar) < FAnimObjectVersion.Type.StoreMarkerNamesOnSkeleton)
            {
                Type = ERigVMParameterType.Invalid;
                Name = new FName(); 
                RegisterIndex = -1;
                ScriptStruct = null;
                ScriptStructPath = new FName();
                return;
            }

            Type = Ar.Read<ERigVMParameterType>();
            Name = Ar.ReadFName();
            RegisterIndex = Ar.Read<int>();
            CPPType = Ar.ReadFString() ?? string.Empty;
            ScriptStructPath = Ar.ReadFName();
        }
    }

    public enum ERigVMParameterType : byte
    {
        Input,
        Output,
        Invalid
    }
}