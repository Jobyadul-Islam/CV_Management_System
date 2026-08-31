using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.ViewModels.Profile;

public class ProjectListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public List<string> Tags { get; set; } = [];

    /// <summary>Only populated by CV rendering, which shows the full project; the Profile tab's own list doesn't need it.</summary>
    public string? DescriptionMarkdown { get; set; }
}

public class ProjectFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateOnly PeriodStart { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? PeriodEnd { get; set; }

    public string? DescriptionMarkdown { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? RowVersion { get; set; }
}
