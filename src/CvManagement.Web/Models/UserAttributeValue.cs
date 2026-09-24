namespace CvManagement.Web.Models;

/// <summary>
/// The single canonical value a user has for a given attribute, across their whole profile
/// and every CV that references that attribute. There is exactly one row per (UserId, AttributeId) --
/// row existence is what "this attribute is on my profile" means; deleting the row removes it from
/// the Info tab. Exactly one of the Value* columns is populated, chosen by Attribute.DataType.
/// Editing a value from inside a CV writes here, which is why the change is visible everywhere.
/// </summary>
public class UserAttributeValue
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int AttributeId { get; set; }
    public AttributeDefinition Attribute { get; set; } = null!;

    public string? ValueString { get; set; }
    public string? ValueText { get; set; }
    public string? ValueImageUrl { get; set; }
    public decimal? ValueNumeric { get; set; }
    public DateOnly? ValueDate { get; set; }
    public DateOnly? ValuePeriodStart { get; set; }
    public DateOnly? ValuePeriodEnd { get; set; }
    public bool? ValueBoolean { get; set; }
    public int? ValueOptionId { get; set; }
    public AttributeOption? ValueOption { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
