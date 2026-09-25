using Microsoft.Extensions.Localization;
using System.Net;
using System.Text;
using CvManagement.Web.Services.Abstractions;

namespace CvManagement.Web.Services.Implementations;

/// <summary>Hand-built SVG (no charting/graphics library needed for a grid of rounded chips) -- kept
/// deterministic and dependency-free so it can be embedded directly in an &lt;img&gt; tag or downloaded.</summary>
public class BadgeSvgRenderer(IStringLocalizer<SharedResource> localizer) : IBadgeSvgRenderer
{
    private const int ChipWidth = 170;
    private const int ChipHeight = 56;
    private const int Gap = 10;
    private const int Padding = 16;
    private const int TitleHeight = 30;
    private const int PerRow = 3;

    public string RenderPanel(IReadOnlyList<EarnedBadge> badges, string displayName)
    {
        var title = localizer["Badge_PanelTitle", displayName].Value;
        var rows = badges.Count == 0 ? 1 : (int)Math.Ceiling(badges.Count / (double)PerRow);
        var columns = Math.Min(PerRow, Math.Max(badges.Count, 1));
        // Wide enough for the badge grid AND the title: with few badges a long name used to be clipped.
        // SVG can't measure text itself, so estimate ~9 px per character at 15 px semibold (slightly generous).
        var titleWidth = Padding * 2 + (int)Math.Ceiling(title.Length * 9.0);
        var width = Math.Max(Padding * 2 + columns * ChipWidth + (columns - 1) * Gap, titleWidth);
        var height = Padding * 2 + TitleHeight + rows * ChipHeight + (rows - 1) * Gap;

        var sb = new StringBuilder();
        sb.Append($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" font-family="Segoe UI, Arial, sans-serif">""");
        sb.Append($"""<rect width="{width}" height="{height}" rx="12" fill="#f8f9fa" stroke="#dee2e6"/>""");
        sb.Append($"""<text x="{Padding}" y="{Padding + 16}" font-size="15" font-weight="600" fill="#212529">{Encode(title)}</text>""");

        if (badges.Count == 0)
        {
            sb.Append($"""<text x="{Padding}" y="{Padding + TitleHeight + 20}" font-size="12" fill="#6c757d">{Encode(localizer["Badge_None"])}</text>""");
        }
        else
        {
            for (var i = 0; i < badges.Count; i++)
            {
                var badge = badges[i];
                var col = i % PerRow;
                var row = i / PerRow;
                var x = Padding + col * (ChipWidth + Gap);
                var y = Padding + TitleHeight + row * (ChipHeight + Gap);

                sb.Append($"""<g transform="translate({x},{y})">""");
                sb.Append($"""<rect width="{ChipWidth}" height="{ChipHeight}" rx="10" fill="{badge.ColorHex}" fill-opacity="0.12" stroke="{badge.ColorHex}" stroke-opacity="0.4"/>""");
                sb.Append($"""<text x="14" y="{ChipHeight / 2 + 7}" font-size="22">{Encode(badge.Icon)}</text>""");
                sb.Append($"""<text x="46" y="{ChipHeight / 2 + 5}" font-size="12" font-weight="600" fill="#212529">{Encode(badge.Label)}</text>""");
                sb.Append("</g>");
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
