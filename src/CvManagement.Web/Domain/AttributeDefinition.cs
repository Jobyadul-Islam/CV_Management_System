using CvManagement.Web.Domain.Enums;

namespace CvManagement.Web.Domain;

/// <summary>
/// Named "AttributeDefinition" (not "Attribute") to avoid colliding with System.Attribute.
/// A single entry in the shared Attribute Library. Reused across profiles (Me/Info),
/// position templates and access rules.
/// </summary>
public class AttributeDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public AttributeCategory Category { get; set; } = null!;

    public AttributeDataType DataType { get; set; }

    /// <summary>
    /// Me-tab built-in attribute (First Name, Last Name, Location, Personal Photo).
    /// Cannot be deleted by anyone, ever -- enforced in the service layer, not just here.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    // Optional per-attribute "tuning" (spec's optional requirement): String/Text length limits and a
    // regex pattern, Numeric min/max. Enforced server-side in ProfileAutoSaveService and surfaced
    // client-side as native HTML5 validation attributes (maxlength/pattern/min/max) -- never trusted
    // from the client alone. Null means "no constraint" for that field.
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? RegexPattern { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }

    public ICollection<AttributeOption> Options { get; set; } = new List<AttributeOption>();
    public ICollection<UserAttributeValue> Values { get; set; } = new List<UserAttributeValue>();
    public ICollection<PositionAttribute> PositionAttributes { get; set; } = new List<PositionAttribute>();
    public ICollection<PositionAccessRule> AccessRules { get; set; } = new List<PositionAccessRule>();
}
