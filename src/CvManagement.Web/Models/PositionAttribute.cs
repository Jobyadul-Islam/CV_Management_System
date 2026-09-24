namespace CvManagement.Web.Models;

/// <summary>Membership + display order of an attribute within a position's CV template.</summary>
public class PositionAttribute
{
    public int Id { get; set; }

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public int AttributeId { get; set; }
    public AttributeDefinition Attribute { get; set; } = null!;

    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
