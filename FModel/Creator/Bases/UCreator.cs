using System;
using System.Windows;
using CUE4Parse.UE4.Assets.Exports;
using FModel.Creator.Bases.FN;
using FModel.Framework;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace FModel.Creator.Bases;

public abstract class UCreator
{
    protected UObject Object { get; }
    protected EIconStyle Style { get; }
    public SKBitmap DefaultPreview { get; set; }
    public SKBitmap Preview { get; set; }
    public SKColor[] Background { get; protected set; }
    public SKColor[] Border { get; protected set; }
    public string DisplayName { get; protected set; }
    public string Description { get; protected set; }
    public int Margin { get; protected set; }
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    public abstract void ParseForInfo();
    public abstract SKBitmap[] Draw();

    protected UCreator(UObject uObject, EIconStyle style)
    {
        DefaultPreview = SKBitmap.Decode(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/T_Placeholder_Item_Image.png"))?.Stream);
        Background = new[] { SKColor.Parse("5BFD00"), SKColor.Parse("003700") };
        Border = new[] { SKColor.Parse("1E8500"), SKColor.Parse("5BFD00") };
        DisplayName = string.Empty;
        Description = string.Empty;
        Width = 512;
        Height = 512;
        Margin = 2;
        Object = uObject;
        Style = style;
    }

    private const int _STARTER_TEXT_POSITION = 380, _NAME_TEXT_SIZE = 45, _BOTTOM_TEXT_SIZE = 15;

    protected readonly SKFont DisplayNameFont = new(Utils.Typefaces.DisplayName, _NAME_TEXT_SIZE);
    protected readonly SKPaint DisplayNamePaint = new()
    {
        IsAntialias = true,
        Color = SKColors.White
    };

    protected readonly SKFont DescriptionFont = new(Utils.Typefaces.Description, 13);
    protected readonly SKPaint DescriptionPaint = new()
    {
        IsAntialias = true,
        Color = SKColors.White
    };

    protected readonly SKPaint ImagePaint = new()
    {
        IsAntialias = true
    };

    private readonly SKPaint _textBackgroundPaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0, 0, 50, 75)
    };

    private readonly SKFont _shortDescriptionFont = new(Utils.Typefaces.Bottom ?? Utils.Typefaces.DisplayName, 13);
    private readonly SKPaint _shortDescriptionPaint = new()
    {
        IsAntialias = true,
        Color = SKColors.White
    };

    public void DrawBackground(SKCanvas c)
    {
        // reverse doesn't affect basic rarities
        if (Background[0] == Background[1]) Background[0] = Border[0];
        Background[0].ToHsl(out _, out _, out var l1);
        Background[1].ToHsl(out _, out _, out var l2);
        var reverse = l1 > l2;

        // border
        c.DrawRect(new SKRect(0, 0, Width, Height),
            new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(Width / 2, Height), new SKPoint(Width, Height / 4), Border, SKShaderTileMode.Clamp)
            });

        if (this is BaseIcon { SeriesBackground: { } } baseIcon)
            c.DrawBitmap(baseIcon.SeriesBackground, new SKRect(baseIcon.Margin, baseIcon.Margin, baseIcon.Width - baseIcon.Margin,
                baseIcon.Height - baseIcon.Margin), ImagePaint);
        else
        {
            switch (Style)
            {
                case EIconStyle.Flat:
                    {
                        c.DrawRect(new SKRect(Margin, Margin, Width - Margin, Height - Margin),
                            new SKPaint
                            {
                                IsAntialias = true,
                                Shader = SKShader.CreateLinearGradient(new SKPoint(Width / 2, Height), new SKPoint(Width, Height / 4),
                                    new[] { Background[reverse ? 0 : 1].WithAlpha(150), Border[0] }, SKShaderTileMode.Clamp)
                            });
                        if (string.IsNullOrEmpty(DisplayName) && string.IsNullOrEmpty(Description)) return;

                        var pathTop = new SKPath { FillType = SKPathFillType.EvenOdd };
                        pathTop.MoveTo(Margin, Margin);
                        pathTop.LineTo(Margin + Width / 17 * 10, Margin);
                        pathTop.LineTo(Margin, Margin + Height / 17);
                        pathTop.Close();
                        c.DrawPath(pathTop, new SKPaint
                        {
                            IsAntialias = true,
                            Color = Background[1].WithAlpha(75)
                        });
                        break;
                    }
                default:
                    {
                        c.DrawRect(new SKRect(Margin, Margin, Width - Margin, Height - Margin),
                            new SKPaint
                            {
                                IsAntialias = true,
                                Shader = SKShader.CreateRadialGradient(new SKPoint(Width / 2, Height / 2), Width / 5 * 4,
                                    new[] { Background[reverse ? 0 : 1], Background[reverse ? 1 : 0] },
                                    SKShaderTileMode.Clamp)
                            });
                        break;
                    }
            }
        }
    }

    protected void DrawPreview(SKCanvas c)
        => c.DrawBitmap(Preview ?? DefaultPreview, new SKRect(Margin, Margin, Width - Margin, Height - Margin), ImagePaint);

    protected void DrawTextBackground(SKCanvas c)
    {
        if (string.IsNullOrEmpty(DisplayName) && string.IsNullOrEmpty(Description)) return;
        switch (Style)
        {
            case EIconStyle.Flat:
                {
                    var pathBottom = new SKPath { FillType = SKPathFillType.EvenOdd };
                    pathBottom.MoveTo(Margin, Height - Margin);
                    pathBottom.LineTo(Margin, Height - Margin - Height / 17 * 2.5f);
                    pathBottom.LineTo(Width - Margin, Height - Margin - Height / 17 * 4.5f);
                    pathBottom.LineTo(Width - Margin, Height - Margin);
                    pathBottom.Close();
                    c.DrawPath(pathBottom, _textBackgroundPaint);
                    break;
                }
            default:
                c.DrawRect(new SKRect(Margin, _STARTER_TEXT_POSITION, Width - Margin, Height - Margin), _textBackgroundPaint);
                break;
        }
    }

    protected void DrawDisplayName(SKCanvas c)
    {
        if (string.IsNullOrWhiteSpace(DisplayName)) return;

        var textBounds = new SKRect();
        DisplayNameFont.MeasureText(DisplayName, out textBounds);
        while (textBounds.Width > Width - Margin * 2)
        {
            DisplayNameFont.Size -= 1;
            DisplayNameFont.MeasureText(DisplayName, out textBounds);
        }

        var shaper = new CustomSKShaper(DisplayNameFont.Typeface);
        var x = Width / 2f;
        var y = _STARTER_TEXT_POSITION + _NAME_TEXT_SIZE;

        SKTextAlign textAlign;
        switch (Style)
        {
            case EIconStyle.Flat:
                textAlign = SKTextAlign.Right;
                x = Width - Margin * 2;
                break;
            default:
                textAlign = SKTextAlign.Center;
                break;
        }

        c.DrawText(DisplayName, x, y, textAlign, DisplayNameFont, DisplayNamePaint);

#if DEBUG
        var halfWidth = textBounds.Width / 2f;
        c.DrawLine(x - halfWidth, 0, x - halfWidth, Width, new SKPaint { Color = SKColors.Blue, IsStroke = true });
        c.DrawLine(x + halfWidth, 0, x + halfWidth, Width, new SKPaint { Color = SKColors.Blue, IsStroke = true });
        c.DrawRect(new SKRect(Margin, _STARTER_TEXT_POSITION, Width - Margin, y), new SKPaint { Color = SKColors.Blue, IsStroke = true });
#endif
    }

    protected void DrawDescription(SKCanvas c)
    {
        if (string.IsNullOrWhiteSpace(Description)) return;

        var maxLine = string.IsNullOrEmpty(DisplayName) ? 8 : 4;
        SKTextAlign side;
        switch (Style)
        {
            case EIconStyle.Flat:
                side = SKTextAlign.Right;
                break;
            default:
                side = SKTextAlign.Center;
                break;
        }

        float x = Margin;
        float y = string.IsNullOrEmpty(DisplayName)
            ? _STARTER_TEXT_POSITION
            : _STARTER_TEXT_POSITION + _NAME_TEXT_SIZE;
        float maxWidth = Width - Margin * 2;

        DrawMultilineText(c, Description, maxWidth, x, y, side, DescriptionFont, DescriptionPaint);
    }

    protected void DrawMultilineText(SKCanvas canvas, string text, float maxWidth, float x, float y, SKTextAlign align, SKFont font, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            var textWidth = font.MeasureText(line);
            var lineHeight = font.Spacing;

            float drawX = x;
            if (align == SKTextAlign.Center)
            {
                drawX += (maxWidth - textWidth) / 2;
            }
            else if (align == SKTextAlign.Right)
            {
                drawX += maxWidth - textWidth;
            }

            // 使用新的 DrawText 重载
            canvas.DrawText(line, drawX, y, font, paint);

            y += lineHeight;
        }
    }

    protected void DrawToBottom(SKCanvas c, SKTextAlign side, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        _shortDescriptionFont.Size = Utils.Typefaces.Bottom == null ? 15 : 13;

        if (side == SKTextAlign.Left)
        {
            _shortDescriptionFont.Typeface = Utils.Typefaces.Bottom ?? Utils.Typefaces.DisplayName;
            var shaper = new CustomSKShaper(_shortDescriptionFont.Typeface);

            c.DrawText(text, Margin * 2.5f, Width - Margin * 2.5f, side, _shortDescriptionFont, _shortDescriptionPaint);
        }
        else
        {
            _shortDescriptionFont.Typeface = Utils.Typefaces.Bottom ?? Utils.Typefaces.Default;

            c.DrawText(text, Margin * 2.5f, Width - Margin * 2.5f, _shortDescriptionFont, _shortDescriptionPaint);
        }
    }
}