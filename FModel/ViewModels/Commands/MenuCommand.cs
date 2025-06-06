using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AdonisUI.Controls;
using FModel.Extensions;
using FModel.Framework;
using FModel.Services;
using FModel.Settings;
using FModel.Views;
using FModel.Views.Resources.Controls;
using Newtonsoft.Json;

namespace FModel.ViewModels.Commands;

public class MenuCommand : ViewModelCommand<ApplicationViewModel>
{
    public MenuCommand(ApplicationViewModel contextViewModel) : base(contextViewModel)
    {
    }

    public override async void Execute(ApplicationViewModel contextViewModel, object parameter)
    {
        switch (parameter)
        {
            case "Directory_Selector":
                contextViewModel.AvoidEmptyGameDirectory(true);
                break;
            case "Directory_AES":
                Helper.OpenWindow<AdonisWindow>("AES管理器", () => new AesManager().Show());
                break;
            case "Directory_Backup":
                Helper.OpenWindow<AdonisWindow>("备份管理器", () => new BackupManager(contextViewModel.CUE4Parse.Provider.ProjectName).Show());
                break;
            case "Directory_ArchivesInfo":
                contextViewModel.CUE4Parse.TabControl.AddTab("档案信息");
                contextViewModel.CUE4Parse.TabControl.SelectedTab.Highlighter = AvalonExtensions.HighlighterSelector("json");
                contextViewModel.CUE4Parse.TabControl.SelectedTab.SetDocumentText(JsonConvert.SerializeObject(contextViewModel.CUE4Parse.GameDirectory.DirectoryFiles, Formatting.Indented), false, false);
                break;
            case "Views_3dViewer":
                contextViewModel.CUE4Parse.SnooperViewer.Run();
                break;
            case "Views_AudioPlayer":
                Helper.OpenWindow<AdonisWindow>("音频播放器", () => new AudioPlayer().Show());
                break;
            case "Views_ImageMerger":
                Helper.OpenWindow<AdonisWindow>("图像合并器", () => new ImageMerger().Show());
                break;
            case "Views_SuperToolbox":
                contextViewModel.OpenSuperToolbox();
                break;
            case "Views_UniversalByteRemover":
                contextViewModel.OpenUniversalByteRemover();
                break;
            case "Views_UniversalBinaryExtractor":
                contextViewModel.OpenUniversalBinaryExtractor();
                break;
            case "Settings":
                Helper.OpenWindow<AdonisWindow>("设置", () => new SettingsView().Show());
                break;
            case "Help_About":
                Helper.OpenWindow<AdonisWindow>("关于", () => new About().Show());
                break;
            case "Help_Donate":
                Process.Start(new ProcessStartInfo { FileName = Constants.DONATE_LINK, UseShellExecute = true });
                break;
            case "Help_Releases":
                Helper.OpenWindow<AdonisWindow>("发布", () => new UpdateView().Show());
                break;
            case "Help_BugsReport":
                Process.Start(new ProcessStartInfo { FileName = Constants.ISSUE_LINK, UseShellExecute = true });
                break;
            case "Help_Discord":
                Process.Start(new ProcessStartInfo { FileName = Constants.DISCORD_LINK, UseShellExecute = true });
                break;
            case "ToolBox_Clear_Logs":
                FLogger.Logger.Text = string.Empty;
                break;
            case "ToolBox_Open_Output_Directory":
                Process.Start(new ProcessStartInfo { FileName = UserSettings.Default.OutputDirectory, UseShellExecute = true });
                break;
            // case "ToolBox_Expand_All":
            //     await ApplicationService.ThreadWorkerView.Begin(cancellationToken =>
            //     {
            //         SetFoldersIsExpanded(contextViewModel.CUE4Parse.AssetsFolder, true, cancellationToken);
            //     });
            //     break;
            case "ToolBox_Collapse_All":
                await ApplicationService.ThreadWorkerView.Begin(cancellationToken =>
                {
                    SetFoldersIsExpanded(contextViewModel.CUE4Parse.AssetsFolder, false, cancellationToken);
                });
                break;
            case TreeItem selectedFolder:
                selectedFolder.IsSelected = false;
                selectedFolder.IsSelected = true;
                break;
        }
    }

    private void SetFoldersIsExpanded(AssetsFolderViewModel root, bool expand, CancellationToken cancellationToken)
    {
        var nodes = new LinkedList<TreeItem>();
        foreach (TreeItem folder in root.Folders)
            nodes.AddLast(folder);

        var current = nodes.First;
        while (current != null)
        {
            var folder = current.Value;

            // Collapse top-down (reduce layout updates)
            if (!expand && folder.IsExpanded)
            {
                folder.IsExpanded = false;
                Thread.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            }

            foreach (var child in folder.Folders)
            {
                nodes.AddLast(child);
            }

            current = current.Next;
        }

        if (!expand) return;

        // Expand bottom-up (reduce layout updates)
        for (var node = nodes.Last; node != null; node = node.Previous)
        {
            node.Value.IsExpanded = true;
            Thread.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
