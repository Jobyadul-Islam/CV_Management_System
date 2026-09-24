namespace CvManagement.Web.ViewModels.Profile;

/// <summary>
/// Wire format for the throttled batch auto-save endpoint. RowVersion null/empty means "no row
/// exists yet for this attribute" (plain insert); otherwise it's the base64 rowversion the client
/// last saw, checked as the EF Core concurrency token on update.
/// </summary>
public class AutoSaveChangeDto
{
    public int AttributeId { get; set; }
    public string? RowVersion { get; set; }

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

public class AutoSaveRequest
{
    public List<AutoSaveChangeDto> Changes { get; set; } = [];
}

public class AutoSaveResultDto
{
    public int AttributeId { get; set; }
    public string Status { get; set; } = "ok"; // ok | conflict | error
    public string? NewRowVersion { get; set; }
    public AutoSaveChangeDto? CurrentValue { get; set; }
    public string? CurrentRowVersion { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Whether the saved value counts as "not filled in" -- drives the red highlight and Publish button live.</summary>
    public bool IsEmpty { get; set; }
}
