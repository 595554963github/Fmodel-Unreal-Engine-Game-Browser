using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using System;

namespace CUE4Parse.UE4.Objects.Niagara
{
    public class FNiagaraDataInterfaceGeneratedFunction
    {
        public FName DefinitionName;
        public string InstanceName = string.Empty;
        public (FName, FName)[] Specifiers = Array.Empty<(FName, FName)>();
        public FNiagaraVariableCommonReference[] VariadicInputs = Array.Empty<FNiagaraVariableCommonReference>();
        public FNiagaraVariableCommonReference[] VariadicOutputs = Array.Empty<FNiagaraVariableCommonReference>();
        public ushort MiscUsageBitMask;

        public FNiagaraDataInterfaceGeneratedFunction(FAssetArchive Ar)
        {
            DefinitionName = Ar.ReadFName();
            InstanceName = Ar.ReadFString() ?? string.Empty;
            Specifiers = Ar.ReadArray(() => (Ar.ReadFName(), Ar.ReadFName())) ?? Array.Empty<(FName, FName)>();

            if (FNiagaraCustomVersion.Get(Ar) >= FNiagaraCustomVersion.Type.AddVariadicParametersToGPUFunctionInfo)
            {
                VariadicInputs = Ar.ReadArray(() => new FNiagaraVariableCommonReference(Ar)) ?? Array.Empty<FNiagaraVariableCommonReference>();
                VariadicOutputs = Ar.ReadArray(() => new FNiagaraVariableCommonReference(Ar)) ?? Array.Empty<FNiagaraVariableCommonReference>();
            }

            if (FNiagaraCustomVersion.Get(Ar) >= FNiagaraCustomVersion.Type.SerializeUsageBitMaskToGPUFunctionInfo)
                MiscUsageBitMask = Ar.Read<ushort>();
        }
    }

    public readonly struct FNiagaraVariableCommonReference : IUStruct
    {
        public readonly FName Name;
        public readonly FPackageIndex UnderlyingType;

        public FNiagaraVariableCommonReference(FAssetArchive Ar)
        {
            Name = Ar.ReadFName();
            UnderlyingType = new FPackageIndex(Ar);
        }
    }
}