using CvManagement.Web.Domain.Enums;
using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// Plain-text rendering of an already-resolved attribute field -- the same per-type logic as
/// _AttributeValueDisplay.cshtml, but as text instead of markup. Shared by the PDF export (QuestPDF
/// has no HTML renderer) and the CSV export (a spreadsheet cell can't hold markup either).
/// </summary>
public static class AttributeValueFormatter
{
    public static string ToPlainText(AttributeValueViewModel field)
    {
        if (field.IsEmpty) return string.Empty;

        return field.DataType switch
        {
            AttributeDataType.String => field.ValueString ?? string.Empty,
            AttributeDataType.Text => field.ValueText ?? string.Empty,
            AttributeDataType.Image => field.ValueImageUrl ?? string.Empty,
            AttributeDataType.Numeric => field.ValueNumeric?.ToString() ?? string.Empty,
            AttributeDataType.Date => field.ValueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            AttributeDataType.Period => $"{field.ValuePeriodStart:yyyy-MM} - {(field.ValuePeriodEnd.HasValue ? field.ValuePeriodEnd.Value.ToString("yyyy-MM") : "present")}",
            AttributeDataType.Boolean => field.ValueBoolean == true ? "Yes" : "No",
            AttributeDataType.OneOfMany => field.Options.FirstOrDefault(o => o.Id == field.ValueOptionId)?.Label ?? string.Empty,
            _ => string.Empty
        };
    }
}
