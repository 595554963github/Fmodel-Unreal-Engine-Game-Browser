namespace FModel.Framework;

public class FStatus : ViewModel
{
    private bool _isReady;
    public bool IsReady
    {
        get => _isReady;
        private set => SetProperty(ref _isReady, value);
    }

    private EStatusKind _kind;
    public EStatusKind Kind
    {
        get => _kind;
        private set
        {
            SetProperty(ref _kind, value);
            IsReady = Kind != EStatusKind.加载中 && Kind != EStatusKind.正在停止;
        }
    }

    private string _label;
    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    public FStatus()
    {
        SetStatus(EStatusKind.加载中);
    }

    public void SetStatus(EStatusKind kind, string label = "")
    {
        Kind = kind;
        UpdateStatusLabel(label);
    }

    public void UpdateStatusLabel(string label, string prefix = null)
    {
        Label = Kind == EStatusKind.加载中 ? $"{prefix ?? Kind.ToString()} {label}".Trim() : Kind.ToString();
    }
}