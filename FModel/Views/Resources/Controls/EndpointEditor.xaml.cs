using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FModel.Extensions;
using FModel.Services;
using FModel.Settings;
using ICSharpCode.AvalonEdit.Document;
using Newtonsoft.Json;

namespace FModel.Views.Resources.Controls;

public partial class EndpointEditor
{
    private readonly EEndpointType _type;
    private bool _isTested;

    public EndpointEditor(EndpointSettings endpoint, string title, EEndpointType type)
    {
        DataContext = endpoint;
        _type = type;
        _isTested = endpoint.IsValid;

        InitializeComponent();

        Title = title;
        TargetResponse.SyntaxHighlighting =
            EndpointResponse.SyntaxHighlighting = AvalonExtensions.HighlighterSelector("json");

        InstructionBox.Text = type switch
        {
            EEndpointType.Aes =>
@"为了使这项工作得以进行,你首先需要了解JSON及其查询语言.如果不了解,请关闭此窗口.如果你的游戏从不更改其AES密钥,甚至都没有加密,也请关闭此窗口.如果你清楚自己在做什么,那你必须知道AES表达式最多支持两个元素.
第一个元素是必需的,它将被指定为主AES密钥.该密钥必须符合密钥的格式,否则你的配置将无效(但不会针对你的文件检查密钥的有效性).此密钥必须是十六进制形式,并且可以不带0x开头.
如果你的游戏使用多个AES密钥，你可以指定第二个元素,它将是你的动态密钥列表.所需的格式是一个对象列表,其中至少包含以下两个变量:
{
    ""指南"": ""档案指南"",
    ""密钥"": ""档案AES密钥""
}",
            EEndpointType.Mapping =>
@"为了使此操作生效,你首先需要了解JSON及其查询语言.如果不了解,请关闭此窗口.如果你的游戏不使用无版本的包属性,也请关闭此窗口.如果你清楚自己在做什么,那你需要知道,映射表达式最多支持两个元素.
第一个元素是必需的,它将被指定为映射下载网址,该网址可以指向各类映射内容,但不能是经过Brotli压缩的.
第二个元素是可选的,它将被指定为映射文件名.如果未指定,该文件名将从网址中获取.",
            _ => ""
        };
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = _isTested && DataContext is EndpointSettings { IsValid: true };
        Close();
    }

    private async void OnSend(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EndpointSettings endpoint) return;

        var body = await ApplicationService.ApiEndpointView.DynamicApi.GetRequestBody(default, endpoint.Url).ConfigureAwait(false);
        Application.Current.Dispatcher.Invoke(delegate
        {
            EndpointResponse.Document ??= new TextDocument();
            EndpointResponse.Document.Text = body.ToString(Formatting.Indented);
        });
    }

    private void OnTest(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EndpointSettings endpoint) return;

        endpoint.TryValidate(ApplicationService.ApiEndpointView.DynamicApi, _type, out var response);
        _isTested = true;

        TargetResponse.Document ??= new TextDocument();
        TargetResponse.Document.Text = JsonConvert.SerializeObject(response, Formatting.Indented);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { IsLoaded: true } ||
            DataContext is not EndpointSettings endpoint) return;
        endpoint.IsValid = false;
    }

    private void OnSyntax(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html", UseShellExecute = true });
    }

    private void OnEvaluator(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://jsonpath.herokuapp.com/", UseShellExecute = true });
    }
}