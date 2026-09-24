using CvManagement.Web.Models.Enums;

namespace CvManagement.Web.Models;

/// <summary>
/// One condition of a Restricted position's eligibility filter. All rules on a position are
/// combined with AND. Only the comparison column matching the attribute's DataType is populated.
/// </summary>
public class PositionAccessRule
{
    public int Id { get; set; }

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public int AttributeId { get; set; }
    public AttributeDefinition Attribute { get; set; } = null!;

    public ComparisonOperator Operator { get; set; }

    public string? ComparisonValueString { get; set; }
    public decimal? ComparisonValueNumeric { get; set; }
    public DateOnly? ComparisonValueDate { get; set; }
    public int? ComparisonOptionId { get; set; }
    public AttributeOption? ComparisonOption { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
