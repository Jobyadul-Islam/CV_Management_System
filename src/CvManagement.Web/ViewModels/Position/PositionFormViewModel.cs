using CvManagement.Web.ViewModels;
using System.ComponentModel.DataAnnotations;
using CvManagement.Web.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvManagement.Web.ViewModels.Position;

public class PositionFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLengthRange), Display(Name = "Field_Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Field_ShortDescription")]
    public string? ShortDescription { get; set; }

    [StringLength(200, ErrorMessage = ValidationMessages.StringLength), Display(Name = "Field_Company")]
    public string? Company { get; set; }

    [Display(Name = "Field_Level")]
    public PositionLevel? Level { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required), Display(Name = "Field_AccessMode")]
    public PositionAccessMode AccessMode { get; set; } = PositionAccessMode.Public;

    [Range(1, 20, ErrorMessage = ValidationMessages.Range), Display(Name = "Field_MaxProjects")]
    public int MaxProjects { get; set; } = 5;

    public string? RowVersion { get; set; }

    public List<int> AttributeIds { get; set; } = [];
    public List<PositionAccessRuleInputModel> AccessRules { get; set; } = [];
    public List<string> ProjectTags { get; set; } = [];

    /// <summary>Id+Name pairs for attributes already selected, for the picker's chip display.</summary>
    public List<(int Id, string Name)> SelectedAttributes { get; set; } = [];

    public List<SelectListItem> LevelOptions { get; set; } = [];
}

public class PositionAccessRuleInputModel
{
    [Required(ErrorMessage = ValidationMessages.Required), Display(Name = "Field_Attribute")]
    public int AttributeId { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required), Display(Name = "Field_Operator")]
    public ComparisonOperator Operator { get; set; }

    public string? ComparisonValueString { get; set; }
    public decimal? ComparisonValueNumeric { get; set; }
    public DateOnly? ComparisonValueDate { get; set; }
    public int? ComparisonOptionId { get; set; }
}
