using System.Text;
using System.Threading.Tasks;
using FModel.Framework;
using FModel.Services;
using FModel.ViewModels.ApiEndpoints.Models;

namespace FModel.ViewModels;

public class AboutViewModel : ViewModel
{
    private ApiEndpointViewModel _apiEndpointView => ApplicationService.ApiEndpointView;

    private string _descriptionLabel;
    public string DescriptionLabel
    {
        get => _descriptionLabel;
        set => SetProperty(ref _descriptionLabel, value);
    }

    private string _contributorsLabel;
    public string ContributorsLabel
    {
        get => _contributorsLabel;
        set => SetProperty(ref _contributorsLabel, value);
    }

    private string _donatorsLabel;
    public string DonatorsLabel
    {
        get => _donatorsLabel;
        set => SetProperty(ref _donatorsLabel, value);
    }

    private string _referencesLabel;
    public string ReferencesLabel
    {
        get => _referencesLabel;
        set => SetProperty(ref _referencesLabel, value);
    }

    public AboutViewModel()
    {

    }

    public async Task Initialize()
    {
        await Task.WhenAll(
            Task.Run(() =>
            {
                DescriptionLabel = "FModel是一款适用于虚幻引擎游戏的资源包浏览器,它以CUE4Parse作为核心解析库,为最新的虚幻4和虚幻5的资源包格式提供了强大的支持,它旨在打造一个现代化且直观的用户界面,具备强大的功能,并配备一套全面的工具,用于预览和转换游戏资源包,让你能够轻松洞悉游戏的内部运行机制..";
                ContributorsLabel = $"FModel的持续存在归功于热情的个人,他们慷慨地贡献了自己的时间和专业知识.个人的贡献,例如{string.Join(", ", "GMatrixGames", "amr", "LongerWarrior", "MinshuG", "InTheShade", "Officer")},以及无数其他的人,无论是过去参与其中的,还是未来将会参与进来的,都确保了这个项目的持续发展和成功.如果你正从FModel中受益,并且希望支持它不断改进,那么请考虑进行捐赠.";
                ReferencesLabel = string.Join(", ",
                    "Adonis UI", "AutoUpdater.NET", "AvalonEdit", "CSCore", "CUE4Parse", "DiscordRichPresence",
                    "EpicManifestParser", "ImGui.NET", "K4os.Compression.LZ4", "Newtonsoft.Json", "NVorbis", "Oodle.NET",
                    "Ookii.Dialogs.Wpf", "OpenTK", "RestSharp", "Serilog", "SixLabors.ImageSharp", "SkiaSharp");
            }),
            Task.Run(() =>
            {
                var donators = _apiEndpointView.FModelApi.GetDonators();
                if (donators == null) return;

                var sb = new StringBuilder();
                sb.AppendJoin<Donator>(", ", donators);
                sb.Append('.');
                DonatorsLabel = sb.ToString();
            })
        ).ConfigureAwait(false);
    }
}
