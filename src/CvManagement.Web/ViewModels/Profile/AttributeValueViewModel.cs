using CvManagement.Web.Models.Enums;
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

    /// <summary>May be left empty: no red highlight, doesn't block publishing a CV.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Empty AND required -- the only case that is highlighted in red and blocks Publish.</summary>
    public bool NeedsValue => IsEmpty && !IsOptional;

    // Optional per-attribute tuning, mirrored client-side as native HTML5 validation attributes
    // (maxlength/pattern/min/max) -- ProfileAutoSaveService re-enforces the same limits server-side.
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? RegexPattern { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

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
