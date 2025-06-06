using System;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using FModel.Framework;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace FModel.Creator.Bases.FN;

public class Reward
{
    private string _rewardQuantity;
    private BaseIcon _theReward;

    public bool HasReward() => _theReward != null;

    public Reward()
    {
        _rewardQuantity = "x0";
    }

    public Reward(int quantity, FName primaryAssetName) : this(quantity, primaryAssetName.Text) { }

    public Reward(int quantity, string assetName) : this()
    {
        _rewardQuantity = $"x{quantity:###,###,###}".Trim();

        if (assetName.Contains(':'))
        {
            var parts = assetName.Split(':');
            if (parts[0].Equals("HomebaseBannerIcon", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Utils.TryLoadObject($"FortniteGame/Content/Items/BannerIcons/{parts[1]}.{parts[1]}", out UObject p))
                {
                    _theReward = new BaseIcon(p, EIconStyle.Default);
                    _theReward.ParseForReward(false);
                    _theReward.Border[0] = SKColors.White;
                    _rewardQuantity = _theReward.DisplayName;
                }
            }
            else GetReward(parts[1]);
        }
        else GetReward(assetName);
    }

    public Reward(UObject uObject)
    {
        _theReward = new BaseIcon(uObject, EIconStyle.Default);
        _theReward.ParseForReward(false);
        _theReward.Border[0] = SKColors.White;
        _rewardQuantity = _theReward.DisplayName;
    }

    private readonly SKPaint _rewardPaint = new() { IsAntialias = true };

    public void DrawQuest(SKCanvas c, SKRect rect)
    {
        var font = new SKFont(Utils.Typefaces.BundleNumber) { Size = 50 };

        if (HasReward())
        {
            var preview = _theReward.Preview ?? _theReward.DefaultPreview;
            var resizedBitmap = preview.Resize(
                new SKImageInfo((int)rect.Height, (int)rect.Height),
                new SKSamplingOptions(SKCubicResampler.Mitchell)
            );

            c.DrawBitmap(resizedBitmap, new SKPoint(rect.Left, rect.Top), _rewardPaint);

            _rewardPaint.Color = _theReward.Border[0];
            _rewardPaint.ImageFilter = SKImageFilter.CreateDropShadow(
                0, 0, 5, 5,
                _theReward.Background[0].WithAlpha(150)
            );

            font.Typeface = _rewardQuantity.StartsWith("x")
                ? Utils.Typefaces.BundleNumber
                : Utils.Typefaces.Bundle;

            while (font.MeasureText(_rewardQuantity) > rect.Width)
            {
                font.Size -= 1;
            }

            var shaper = new CustomSKShaper(font.Typeface);
            var result = shaper.Shape(_rewardQuantity, _rewardPaint);
            var position = new SKPoint(rect.Left + rect.Height + 25, rect.MidY + 20);

            foreach (var point in result.Points)
            {
                c.DrawText(_rewardQuantity, position.X + point.X, position.Y + point.Y, font, _rewardPaint);
            }
        }
        else
        {
            _rewardPaint.Color = SKColors.White;

            c.DrawText(
                "No Reward",
                rect.Left,
                rect.MidY + 20,
                font,
                _rewardPaint
            );
        }
    }

    public void DrawSeasonWin(SKCanvas c, int size)
    {
        if (!HasReward()) return;
        var preview = _theReward.Preview ?? _theReward.DefaultPreview;
        var resizedBitmap = preview.Resize(
            new SKImageInfo(size, size),
            new SKSamplingOptions(SKCubicResampler.Mitchell)
        );
        c.DrawBitmap(resizedBitmap, new SKPoint(0, 0), _rewardPaint);
    }

    public void DrawSeason(SKCanvas c, int x, int y, int areaSize)
    {
        if (!HasReward()) return;

        _rewardPaint.Color = SKColor.Parse("#0F5CAF");
        c.DrawRect(new SKRect(x, y, x + areaSize, y + areaSize), _rewardPaint);

        var resizedBitmap = _theReward.Preview.Resize(
            new SKImageInfo(areaSize, areaSize),
            new SKSamplingOptions(SKCubicResampler.Mitchell)
        );
        c.DrawBitmap(resizedBitmap, new SKPoint(x, y), _rewardPaint);

        _rewardPaint.Color = _theReward.Background[0];
        var pathBottom = new SKPath { FillType = SKPathFillType.EvenOdd };
        pathBottom.MoveTo(x, y + areaSize);
        pathBottom.LineTo(x, y + areaSize - areaSize / 25 * 2.5f);
        pathBottom.LineTo(x + areaSize, y + areaSize - areaSize / 25 * 4.5f);
        pathBottom.LineTo(x + areaSize, y + areaSize);
        pathBottom.Close();
        c.DrawPath(pathBottom, _rewardPaint);
    }

    private void GetReward(string trigger)
    {
        switch (trigger.ToLower())
        {
            default:
                {
                    var path = Utils.GetFullPath($"FortniteGame/(?:Content/Athena|Content/Items|Plugins/GameFeatures)/.*?/{trigger}.uasset");
                    if (!string.IsNullOrWhiteSpace(path) && Utils.TryLoadObject(path.Replace("uasset", trigger), out UObject d))
                    {
                        _theReward = new BaseIcon(d, EIconStyle.Default);
                        _theReward.ParseForReward(false);
                        _theReward.Border[0] = SKColors.White;
                        _rewardQuantity = $"{_theReward.DisplayName} ({_rewardQuantity})";
                    }
                    break;
                }
        }
    }
}