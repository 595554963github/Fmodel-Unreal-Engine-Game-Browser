using System;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.FileProvider
{
    public class StreamedFileProvider : AbstractVfsFileProvider
    {
        public string LiveGame { get; }

        [Obsolete("将其他构造函数与显式字符串比较器一起使用")]
        public StreamedFileProvider(string liveGame, bool isCaseInsensitive = false, VersionContainer? versions = null)
            : this(liveGame, versions, isCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal) { }
        public StreamedFileProvider(string liveGame, VersionContainer? versions = null, StringComparer? pathComparer = null) : base(versions, pathComparer)
        {
            LiveGame = liveGame;
        }

        public override void Initialize()
        {
            // there should be nothing here ig
        }
    }
}
