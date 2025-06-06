using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;
using SkiaSharp;

namespace FModel.Creator.Bases.FN;

public class BaseItemAccessToken : UCreator
{
    private readonly SKBitmap _locked, _unlocked;
    private string _unlockedDescription, _exportName;
    private BaseIcon _icon;

    private readonly SKFont _displayNameFont;
    private readonly SKFont _descriptionFont;
    private readonly SKPaint _displayNamePaint;
    private readonly SKPaint _descriptionPaint;

    public BaseItemAccessToken(UObject uObject, EIconStyle style) : base(uObject, style)
    {
        _unlocked = Utils.GetBitmap("FortniteGame/Content/UI/Foundation/Textures/Icons/Locks/T-Icon-Unlocked-128.T-Icon-Unlocked-128").Resize(24);
        _locked = Utils.GetBitmap("FortniteGame/Content/UI/Foundation/Textures/Icons/Locks/T-Icon-Lock-128.T-Icon-Lock-128").Resize(24);

        _displayNameFont = new SKFont(SKTypeface.Default, 45);
        _descriptionFont = new SKFont(SKTypeface.Default, 20);

        _displayNamePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White
        };

        _descriptionPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(200)
        };
    }

    public override void ParseForInfo()
    {
        if (Object.TryGetValue(out FPackageIndex accessItem, "access_item") &&
            Utils.TryGetPackageIndexExport(accessItem, out UObject uObject))
        {
            _exportName = uObject.Name;
            _icon = new BaseIcon(uObject, EIconStyle.Default);
            _icon.ParseForReward(false);
        }

        if (Object.TryGetValue(out FText displayName, "DisplayName", "ItemName") && displayName.Text != "TBD")
            DisplayName = displayName.Text;
        else
            DisplayName = _icon?.DisplayName;

        Description = Object.TryGetValue(out FText description, "Description", "ItemDescription") ? description.Text : _icon?.Description;
        if (Object.TryGetValue(out FText unlockDescription, "UnlockDescription")) _unlockedDescription = unlockDescription.Text;
    }

    public override SKBitmap[] Draw()
    {
        var ret = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var c = new SKCanvas(ret);

        switch (Style)
        {
            case EIconStyle.NoBackground:
                Preview = _icon.Preview;
                DrawPreview(c);
                break;
            case EIconStyle.NoText:
                Preview = _icon.Preview;
                _icon.DrawBackground(c);
                DrawPreview(c);
                break;
            default:
                _icon.DrawBackground(c);
                DrawInformation(c);
                DrawToBottom(c, SKTextAlign.Right, _exportName);
                break;
        }

        return new[] { ret };
    }

    private void DrawInformation(SKCanvas c)
    {
        var size = 45f;
        var left = Width / 2f;

        _displayNameFont.Size = size;

        while (_displayNameFont.MeasureText(DisplayName) > Width - _icon.Margin * 2)
        {
            _displayNameFont.Size = size -= 2f;
        }

        c.DrawText(DisplayName, left, _icon.Margin * 8 + size, _displayNameFont, _displayNamePaint);

        float topBase = _icon.Margin + size * 2;
        if (!string.IsNullOrEmpty(_unlockedDescription))
        {
            c.DrawBitmap(_locked, new SKRect(50, topBase, 50 + _locked.Width, topBase + _locked.Height), ImagePaint);

            Utils.DrawMultilineText(
                c,
                _unlockedDescription,
                20,
                10,
                SKTextAlign.Left,
                new SKRect(70 + _locked.Width, topBase + 10, Width - 50, 256),
                _descriptionFont,
                _descriptionPaint,
                out topBase
            );

            Utils.DrawMultilineText(
                c,
                Description,
                20,
                10,
                SKTextAlign.Left,
                new SKRect(70 + _unlocked.Width, topBase + 10, Width - 50, 256),
                _descriptionFont,
                _descriptionPaint,
                out topBase
            );
        }
    }
}