using System;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Serilog;

namespace CUE4Parse_Conversion.Textures.BC;

public static class DetexHelper
{
    private const string MANIFEST_URL = "CUE4Parse_Conversion.Resources.Detex.dll";
    public const string DLL_NAME = "Detex.dll";

    private static Detex? Instance { get; set; }

    /// <summary>
    /// 使用给定路径初始化Detex库
    /// </summary>
    public static void Initialize(string path)
    {
        Instance?.Dispose();
        if (File.Exists(path))
            Instance = new Detex(path);
    }

    /// <summary>
    /// 使用现有实例初始化Detex
    /// </summary>
    public static void Initialize(Detex instance)
    {
        Instance?.Dispose();
        Instance = instance;
    }

    /// <summary>
    /// 加载Detex库DLL
    /// </summary>
    public static bool LoadDll(string? path = null)
    {
        if (File.Exists(path ?? DLL_NAME))
            return true;
        return LoadDllAsync(path).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 使用Detex库解码压缩数据
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] DecodeDetexLinear(byte[] inp, int width, int height, bool isFloat, DetexTextureFormat inputFormat, DetexPixelFormat outputPixelFormat)
    {
        if (Instance is null)
        {
            const string message = "Detex解压失败:尚未初始化";
            throw new Exception(message);
        }

        var dst = new byte[width * height * (isFloat ? 16 : 4)];
        Instance.DecodeDetexLinear(inp, dst, width, height, inputFormat, outputPixelFormat);
        return dst;
    }

    /// <summary>
    /// 从资源异步加载Detex.DLL
    /// </summary>
    public static async Task<bool> LoadDllAsync(string? path)
    {
        try
        {
            var dllPath = path ?? DLL_NAME;

            if (File.Exists(dllPath))
            {
                Log.Information($"Detex.DLL已存在于\"{dllPath}\"。");
                return true;
            }

            await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(MANIFEST_URL);
            if (stream == null)
            {
                throw new MissingManifestResourceException("无法在嵌入式资源中找到Detex.dll");
            }

            await using var dllFs = File.Create(dllPath);
            await stream.CopyToAsync(dllFs).ConfigureAwait(false);

            Log.Information($"成功从嵌入式资源加载Detex.DLL到\"{dllPath}\"");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载Detex.DLL时捕获到未处理异常");
            return false;
        }
    }
}