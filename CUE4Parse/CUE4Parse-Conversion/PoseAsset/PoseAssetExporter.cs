using System;
using System.IO;
using CUE4Parse_Conversion.PoseAsset.UEFormat;
using CUE4Parse.UE4.Objects.Engine.Animation;
using CUE4Parse.UE4.Writers;
using Serilog;

namespace CUE4Parse_Conversion.PoseAsset;

public class PoseAssetExporter : ExporterBase
{
    private readonly PoseAsset _poseAsset = new PoseAsset(string.Empty, Array.Empty<byte>());
    public PoseAsset PoseAsset => _poseAsset;

    public PoseAssetExporter(UPoseAsset poseAsset, ExporterOptions options) : base(poseAsset, options)
    {
        if (poseAsset.TryConvert(out var convertedPoseAsset))
        {
            using var Ar = new FArchiveWriter();
            string ext;
            switch (Options.PoseFormat)
            {
                case EPoseFormat.UEFormat:
                    ext = "uepose";
                    new UEPose(poseAsset.Name, convertedPoseAsset, Options).Save(Ar);
                    _poseAsset = new PoseAsset($"{GetExportSavePath()}.{ext}", Ar.GetBuffer());
                    return;
            }
        }

        Log.Warning($"PoseAsset '{ExportName}'转换失败,使用默认空资源");
    }

    public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
    {
        throw new NotImplementedException();
    }

    public override bool TryWriteToZip(out byte[] zipFile)
    {
        throw new NotImplementedException();
    }

    public override void AppendToZip()
    {
        throw new NotImplementedException();
    }
}