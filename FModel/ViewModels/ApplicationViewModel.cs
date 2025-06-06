using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.VirtualFileSystem;
using FModel.Framework;
using FModel.Services;
using FModel.Settings;
using FModel.ViewModels.Commands;
using FModel.Views;
using FModel.Views.Resources.Controls;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using super_toolbox;
using UniversalByteRemover;
using UniversalFileExtractor;
namespace FModel.ViewModels;

public class ApplicationViewModel : ViewModel
{
    private EBuildKind _build;
    public EBuildKind Build
    {
        get => _build;
        private init
        {
            SetProperty(ref _build, value);
            RaisePropertyChanged(nameof(TitleExtra));
        }
    }

    private FStatus _status;
    public FStatus Status
    {
        get => _status;
        private init => SetProperty(ref _status, value);
    }

    public RightClickMenuCommand RightClickMenuCommand => _rightClickMenuCommand ??= new RightClickMenuCommand(this);
    private RightClickMenuCommand _rightClickMenuCommand;
    public MenuCommand MenuCommand => _menuCommand ??= new MenuCommand(this);
    private MenuCommand _menuCommand;
    public CopyCommand CopyCommand => _copyCommand ??= new CopyCommand(this);
    private CopyCommand _copyCommand;

    public string InitialWindowTitle => $"FModel ({Constants.APP_SHORT_COMMIT_ID})";
    public string GameDisplayName => CUE4Parse.Provider.GameDisplayName ?? "δ֪";
    public string TitleExtra => $"({UserSettings.Default.CurrentDir.UeVersion}){(Build != EBuildKind.Release ? $" ({Build})" : "")}";

    public LoadingModesViewModel LoadingModes { get; }
    public CustomDirectoriesViewModel CustomDirectories { get; }
    public CUE4ParseViewModel CUE4Parse { get; }
    public SettingsViewModel SettingsView { get; }
    public AesManagerViewModel AesManager { get; }
    public AudioPlayerViewModel AudioPlayer { get; }

    public ApplicationViewModel()
    {
        Status = new FStatus();
#if DEBUG
        Build = EBuildKind.Debug;
#elif RELEASE
        Build = EBuildKind.Release;
#else
        Build = EBuildKind.Unknown;
#endif
        LoadingModes = new LoadingModesViewModel();

        UserSettings.Default.CurrentDir = AvoidEmptyGameDirectory(false);
        if (UserSettings.Default.CurrentDir is null)
        {
            Environment.Exit(0);
        }

        CUE4Parse = new CUE4ParseViewModel();
        CUE4Parse.Provider.VfsRegistered += (sender, count) =>
        {
            if (sender is not IAesVfsReader reader) return;
            Status.UpdateStatusLabel($"{count} Archives ({reader.Name})", "Registered");
            CUE4Parse.GameDirectory.Add(reader);
        };
        CUE4Parse.Provider.VfsMounted += (sender, count) =>
        {
            if (sender is not IAesVfsReader reader) return;
            Status.UpdateStatusLabel($"{count:N0} Packages ({reader.Name})", "Mounted");
            CUE4Parse.GameDirectory.Verify(reader);
        };
        CUE4Parse.Provider.VfsUnmounted += (sender, _) =>
        {
            if (sender is not IAesVfsReader reader) return;
            CUE4Parse.GameDirectory.Disable(reader);
        };

        CustomDirectories = new CustomDirectoriesViewModel();
        SettingsView = new SettingsViewModel();
        AesManager = new AesManagerViewModel(CUE4Parse);
        AudioPlayer = new AudioPlayerViewModel();

        Status.SetStatus(EStatusKind.就绪);
    }

    public DirectorySettings AvoidEmptyGameDirectory(bool bAlreadyLaunched)
    {
        var gameDirectory = UserSettings.Default.GameDirectory;
        if (!bAlreadyLaunched && UserSettings.Default.PerDirectory.TryGetValue(gameDirectory, out var currentDir))
            return currentDir;

        var gameLauncherViewModel = new GameSelectorViewModel(gameDirectory);
        var result = new DirectorySelector(gameLauncherViewModel).ShowDialog();
        if (!result.HasValue || !result.Value) return null;

        UserSettings.Default.GameDirectory = gameLauncherViewModel.SelectedDirectory.GameDirectory;
        if (!bAlreadyLaunched || UserSettings.Default.CurrentDir.Equals(gameLauncherViewModel.SelectedDirectory))
            return gameLauncherViewModel.SelectedDirectory;

        UserSettings.Default.CurrentDir = gameLauncherViewModel.SelectedDirectory;
        RestartWithWarning();
        return null;
    }

    private void ExecuteMenuCommand(string parameter)
    {
        switch (parameter)
        {
            case "Views_SuperToolbox":
                OpenSuperToolbox();
                break;
            case "Views_UniversalByteRemover":
                OpenUniversalByteRemover();
                break;
            case "Views_UniversalBinaryExtractor":
                OpenUniversalBinaryExtractor();
                break;
        }
    }

    public void OpenSuperToolbox()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var form = new SuperToolbox();
                form.Show();

                form.FormClosed += (sender, args) => ((Form)sender)?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开超级工具箱失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }
    public void OpenUniversalByteRemover()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var form = new ByteRemover();
                form.Show();

                form.FormClosed += (sender, args) => ((Form)sender)?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开万能字节移除器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }
    public void OpenUniversalBinaryExtractor()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var form = new FileExtractor();
                form.Show();

                form.FormClosed += (sender, args) => ((Form)sender)?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开万能二进制提取器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    public void RestartWithWarning()
    {
        MessageBox.Show("看起来你改变了什么.\nModel将重新启动以应用您的更改.", "需要重新启动", MessageBoxButton.OK, MessageBoxImage.Warning);
        Restart();
    }

    public void Restart()
    {
        var path = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
        if (path.EndsWith(".dll"))
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            }.Start();
        }
        else if (path.EndsWith(".exe"))
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            }.Start();
        }

        System.Windows.Application.Current.Shutdown();
    }

    public async Task UpdateProvider(bool isLaunch)
    {
        if (!isLaunch && !AesManager.HasChange) return;

        CUE4Parse.ClearProvider();
        await ApplicationService.ThreadWorkerView.Begin(cancellationToken =>
        {
            var aes = AesManager.AesKeys.Select(x =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var k = x.Key.Trim();
                if (k.Length != 66) k = Constants.ZERO_64_CHAR;
                return new KeyValuePair<FGuid, FAesKey>(x.Guid, new FAesKey(k));
            });

            CUE4Parse.LoadVfs(aes);
            AesManager.SetAesKeys();
        });
        RaisePropertyChanged(nameof(GameDisplayName));
    }

    public static async Task InitVgmStream()
    {
        var vgmZipFilePath = Path.Combine(UserSettings.Default.OutputDirectory, ".data", "vgmstream-win.zip");
        if (File.Exists(vgmZipFilePath)) return;

        await ApplicationService.ApiEndpointView.DownloadFileAsync("https://github.com/vgmstream/vgmstream/releases/download/r2023/vgmstream-win64.zip\r\n", vgmZipFilePath);
        if (new FileInfo(vgmZipFilePath).Length > 0)
        {
            var zipDir = Path.GetDirectoryName(vgmZipFilePath)!;
            await using var zipFs = File.OpenRead(vgmZipFilePath);
            using var zip = new ZipArchive(zipFs, ZipArchiveMode.Read);

            foreach (var entry in zip.Entries)
            {
                var entryPath = Path.Combine(zipDir, entry.FullName);
                await using var entryFs = File.Create(entryPath);
                await using var entryStream = entry.Open();
                await entryStream.CopyToAsync(entryFs);
            }
        }
        else
        {
            FLogger.Append(ELog.Error, () => FLogger.Text("无法下载VgmStream", Constants.WHITE, true));
        }
    }

    public static async Task InitImGuiSettings(bool forceDownload)
    {
        var imgui = "imgui.ini";
        var imguiPath = Path.Combine(UserSettings.Default.OutputDirectory, ".data", imgui);

        if (File.Exists(imgui)) File.Move(imgui, imguiPath, true);
        if (File.Exists(imguiPath) && !forceDownload) return;

        await ApplicationService.ApiEndpointView.DownloadFileAsync($"https://cdn.fmodel.app/d/configurations/{imgui}", imguiPath);
        if (new FileInfo(imguiPath).Length == 0)
        {
            FLogger.Append(ELog.Error, () => FLogger.Text("无法下载ImGui设置", Constants.WHITE, true));
        }
    }

    public static async ValueTask InitOodle()
    {
        var oodlePath = Path.Combine(UserSettings.Default.OutputDirectory, ".data", OodleHelper.OODLE_DLL_NAME);
        if (File.Exists(OodleHelper.OODLE_DLL_NAME))
        {
            File.Move(OodleHelper.OODLE_DLL_NAME, oodlePath, true);
        }
        else if (!File.Exists(oodlePath))
        {
            await OodleHelper.DownloadOodleDllAsync(oodlePath);
        }

        OodleHelper.Initialize(oodlePath);
    }

    public static async ValueTask InitZlib()
    {
        var zlibPath = Path.Combine(UserSettings.Default.OutputDirectory, ".data", ZlibHelper.DLL_NAME);
        if (!File.Exists(zlibPath))
        {
            await ZlibHelper.DownloadDllAsync(zlibPath);
        }

        ZlibHelper.Initialize(zlibPath);
    }
}