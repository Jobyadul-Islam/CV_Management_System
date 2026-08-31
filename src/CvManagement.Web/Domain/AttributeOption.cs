namespace CvManagement.Web.Domain;

/// <summary>Choice available for a OneOfMany (dropdown) attribute.</summary>
public class AttributeOption
{
    public int Id { get; set; }

    public int AttributeId { get; set; }
    public AttributeDefinition Attribute { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
