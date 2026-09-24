using Microsoft.Extensions.Localization;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// Plain-text rendering of an already-resolved attribute field -- the same per-type logic as
/// _AttributeValueDisplay.cshtml, but as text instead of markup. Shared by the PDF export (QuestPDF
/// has no HTML renderer) and the CSV export (a spreadsheet cell can't hold markup either).
/// </summary>
public static class AttributeValueFormatter
{
    public static string ToPlainText(AttributeValueViewModel field, IStringLocalizer localizer)
    {
        if (field.IsEmpty) return string.Empty;

        return field.DataType switch
        {
            AttributeDataType.String => field.ValueString ?? string.Empty,
            AttributeDataType.Text => field.ValueText ?? string.Empty,
            AttributeDataType.Image => field.ValueImageUrl ?? string.Empty,
            // Invariant: under the Russian UI culture "3.5" would otherwise become "3,5", which splits a CSV cell.
            AttributeDataType.Numeric => field.ValueNumeric?.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            AttributeDataType.Date => field.ValueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            AttributeDataType.Period => $"{field.ValuePeriodStart:yyyy-MM} - {(field.ValuePeriodEnd.HasValue ? field.ValuePeriodEnd.Value.ToString("yyyy-MM") : localizer["Profile_Present"])}",
            AttributeDataType.Boolean => field.ValueBoolean == true ? localizer["Common_Yes"] : localizer["Common_No"],
            AttributeDataType.OneOfMany => field.Options.FirstOrDefault(o => o.Id == field.ValueOptionId)?.Label ?? string.Empty,
            _ => string.Empty
        };
    }
}
