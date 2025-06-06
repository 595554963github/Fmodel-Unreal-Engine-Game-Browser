using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Core.i18N;
using SkiaSharp;

namespace FModel.Creator.Bases.FN;

public class BaseUserControl : UCreator
{
    private List<Options> _optionValues = new();

    private readonly SKFont _displayNameFont;
    private readonly SKFont _descriptionFont;
    private readonly SKPaint _displayNamePaint;
    private readonly SKPaint _descriptionPaint;

    public BaseUserControl(UObject uObject, EIconStyle style) : base(uObject, style)
    {
        Width = 512;
        Height = 128;
        Margin = 32;

        // Initialize fonts and paints properly
        _displayNameFont = new SKFont(Utils.Typefaces.DisplayName, 45)
        {
            Edging = SKFontEdging.Antialias
        };

        _descriptionFont = new SKFont(Utils.Typefaces.DisplayName, 25)
        {
            Edging = SKFontEdging.Antialias
        };

        _displayNamePaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        _descriptionPaint = new SKPaint
        {
            Color = SKColor.Parse("88DBFF"),
            IsAntialias = true
        };
    }

    public override void ParseForInfo()
    {
        if (Object.TryGetValue(out FText optionDisplayName, "optionDisplayName", "OptionText"))
        {
            DisplayName = optionDisplayName.Text.ToUpperInvariant();
        }

        if (Object.TryGetValue(out FText optionDescription, "OptionDescription", "OptionToolTip"))
        {
            Description = optionDescription.Text;

            if (!string.IsNullOrWhiteSpace(Description))
            {
                var textLines = Utils.SplitLines(Description, _descriptionFont, Width - Margin);
                Height += (int)_descriptionFont.Size * textLines.Count;
                Height += (int)_descriptionFont.Size;
            }
        }
    }

    public override SKBitmap[] Draw()
    {
        var ret = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var c = new SKCanvas(ret);

        DrawBackground(c);
        DrawInformation(c);

        return new[] { ret };
    }

    private new void DrawBackground(SKCanvas c)
    {
        c.DrawRect(new SKRect(0, 0, Width, Height),
            new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(Width / 2, Height),
                    new SKPoint(Width, Height / 4),
                    new[] { SKColor.Parse("01369C"), SKColor.Parse("1273C8") },
                    SKShaderTileMode.Clamp)
            });
    }

    private void DrawInformation(SKCanvas c)
    {
        while (_displayNameFont.MeasureText(DisplayName) > Width - Margin * 2)
        {
            _displayNameFont.Size -= 2;
        }

        c.DrawText(
            DisplayName,
            Margin,
            Margin + _displayNameFont.Size,
            _displayNameFont,
            _displayNamePaint
        );

        float y = Margin;
        if (!string.IsNullOrEmpty(DisplayName)) y += _displayNameFont.Size;
        if (!string.IsNullOrEmpty(Description)) y += _descriptionFont.Size + Margin / 2F;

        float top = y;
        DrawMultilineText(c, Description, Width - Margin * 2, Margin, y, _descriptionFont, _descriptionPaint);

        foreach (var option in _optionValues)
        {
            option.Draw(c, Margin, Width, ref top);
        }
    }

    private void DrawMultilineText(SKCanvas canvas, string text, float maxWidth, float x, float y, SKFont font, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            var textWidth = font.MeasureText(line);

            if (textWidth > maxWidth)
            {
                var words = line.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = currentLine + (currentLine == "" ? "" : " ") + word;
                    var testWidth = font.MeasureText(testLine);

                    if (testWidth <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                        {
                            canvas.DrawText(
                                currentLine,
                                x,
                                y,
                                font,
                                paint
                            );
                            y += font.Metrics.CapHeight + font.Metrics.Descent;
                        }
                        currentLine = word;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    canvas.DrawText(
                        currentLine,
                        x,
                        y,
                        font,
                        paint
                    );
                    y += font.Metrics.CapHeight + font.Metrics.Descent;
                }
            }
            else
            {
                canvas.DrawText(
                    line,
                    x,
                    y,
                    font,
                    paint
                );
                y += font.Metrics.CapHeight + font.Metrics.Descent;
            }
        }
    }

    public class Options
    {
        private const int _SPACE = 5;
        private const int _HEIGHT = 30;

        private readonly SKFont _optionFont;
        private readonly SKPaint _optionPaint;

        public string Option;
        public SKColor Color = SKColor.Parse("55C5FC").WithAlpha(150);

        public Options()
        {
            _optionFont = new SKFont(Utils.Typefaces.DisplayName, 20)
            {
                Edging = SKFontEdging.Antialias
            };

            _optionPaint = new SKPaint
            {
                Color = SKColor.Parse("EEFFFF"),
                IsAntialias = true
            };
        }

        public void Draw(SKCanvas c, int margin, int width, ref float top)
        {
            var rectPaint = new SKPaint
            {
                IsAntialias = true,
                Color = Color
            };

            c.DrawRect(new SKRect(margin, top, width - margin, top + _HEIGHT), rectPaint);

            c.DrawText(
                Option,
                margin + _SPACE * 2,
                top + _optionFont.Size * 1.1f,
                _optionFont,
                _optionPaint
            );

            top += _HEIGHT + _SPACE;
        }
    }
}