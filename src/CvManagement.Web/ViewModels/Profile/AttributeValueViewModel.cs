using CvManagement.Web.Domain.Enums;
using CvManagement.Web.ViewModels.Attribute;

namespace CvManagement.Web.ViewModels.Profile;

/// <summary>
/// One editable attribute field -- shared shape for the Me/Info tabs (Phase 5) and CV rendering
/// (Phase 6), since both are "render this user's value for this attribute, editable in place."
/// </summary>
public class AttributeValueViewModel
{
    public int AttributeId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; }
    public List<AttributePickerOptionViewModel> Options { get; set; } = [];
    public string? RowVersion { get; set; }
    public bool IsEmpty { get; set; }

    public string? ValueString { get; set; }
    public string? ValueText { get; set; }
    public string? ValueImageUrl { get; set; }
    public decimal? ValueNumeric { get; set; }
    public DateOnly? ValueDate { get; set; }
    public DateOnly? ValuePeriodStart { get; set; }
    public DateOnly? ValuePeriodEnd { get; set; }
    public bool? ValueBoolean { get; set; }
    public int? ValueOptionId { get; set; }
}
