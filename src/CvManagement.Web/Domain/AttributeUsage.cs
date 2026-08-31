namespace CvManagement.Web.Domain;

/// <summary>
/// Tracks when a user last selected an attribute (adding it to a profile, a position template, or an
/// access rule), backing the attribute picker's "recently used" list. Upserted on selection, never
/// shown to other users -- purely a per-user convenience, so no RowVersion.
/// </summary>
public class AttributeUsage
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int AttributeId { get; set; }
    public AttributeDefinition Attribute { get; set; } = null!;

    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
