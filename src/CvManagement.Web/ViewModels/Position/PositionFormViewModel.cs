using System.ComponentModel.DataAnnotations;
using CvManagement.Web.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvManagement.Web.ViewModels.Position;

public class PositionFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ShortDescription { get; set; }

    [StringLength(200)]
    public string? Company { get; set; }

    public PositionLevel? Level { get; set; }

    [Required]
    public PositionAccessMode AccessMode { get; set; } = PositionAccessMode.Public;

    [Range(1, 20)]
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
    [Required]
    public int AttributeId { get; set; }

    [Required]
    public ComparisonOperator Operator { get; set; }

    public string? ComparisonValueString { get; set; }
    public decimal? ComparisonValueNumeric { get; set; }
    public DateOnly? ComparisonValueDate { get; set; }
    public int? ComparisonOptionId { get; set; }
}
