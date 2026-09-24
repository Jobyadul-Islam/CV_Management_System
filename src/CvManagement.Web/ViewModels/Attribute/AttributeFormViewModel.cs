using CvManagement.Web.ViewModels;
using System.ComponentModel.DataAnnotations;
using CvManagement.Web.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvManagement.Web.ViewModels.Attribute;

public class AttributeFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLengthRange), Display(Name = "Field_Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Field_Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required), Display(Name = "Field_Category")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required), Display(Name = "Field_DataType")]
    public AttributeDataType DataType { get; set; }

    public bool IsBuiltIn { get; set; }

    // Optional validation tuning -- only meaningful for String/Text (length + regex) and Numeric
    // (min/max) attributes; AttributeService ignores/nulls these out for other data types.
    [Range(0, 100_000, ErrorMessage = ValidationMessages.Range), Display(Name = "Field_MinLength")]
    public int? MinLength { get; set; }

    [Range(0, 100_000, ErrorMessage = ValidationMessages.Range), Display(Name = "Field_MaxLength")]
    public int? MaxLength { get; set; }

    [StringLength(500, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Field_RegexPattern")]
    public string? RegexPattern { get; set; }

    [Display(Name = "Field_MinValue")]
    public decimal? MinValue { get; set; }

    [Display(Name = "Field_MaxValue")]
    public decimal? MaxValue { get; set; }

    public List<AttributeOptionInputModel> Options { get; set; } = [];

    /// <summary>Base64-encoded rowversion; null/empty on create.</summary>
    public string? RowVersion { get; set; }

    public List<SelectListItem> CategoryOptions { get; set; } = [];
}

public class AttributeOptionInputModel
{
    public int Id { get; set; }

    // Nullable, not [Required]: a freshly-added blank row (before the user types a label) must not
    // fail validation for the whole form -- AttributeService filters out blank-label rows itself.
    [StringLength(200, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Field_OptionLabel")]
    public string? Label { get; set; }

    public int SortOrder { get; set; }

    public string? RowVersion { get; set; }
}
