using System.ComponentModel.DataAnnotations;
using CvManagement.Web.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvManagement.Web.ViewModels.Attribute;

public class AttributeFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required, Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required]
    public AttributeDataType DataType { get; set; }

    public bool IsBuiltIn { get; set; }

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
    [StringLength(200)]
    public string? Label { get; set; }

    public int SortOrder { get; set; }

    public string? RowVersion { get; set; }
}
