namespace CvManagement.Web.Domain.Enums;

/// <summary>
/// Which ComparisonOperators are meaningful for each AttributeDataType. Drives both the access-rule
/// builder's operator dropdown (client-side, via a JSON dump of this table) and could gate server-side
/// validation if a submitted (AttributeId, Operator) pair needs rechecking.
/// </summary>
public static class OperatorCatalog
{
    public static readonly IReadOnlyDictionary<AttributeDataType, ComparisonOperator[]> AllowedOperators =
        new Dictionary<AttributeDataType, ComparisonOperator[]>
        {
            [AttributeDataType.String] =
            [
                ComparisonOperator.Equals, ComparisonOperator.NotEquals, ComparisonOperator.Contains,
                ComparisonOperator.StartsWith, ComparisonOperator.IsSet, ComparisonOperator.IsNotSet
            ],
            [AttributeDataType.Text] = [ComparisonOperator.Contains, ComparisonOperator.IsSet, ComparisonOperator.IsNotSet],
            [AttributeDataType.Image] = [ComparisonOperator.IsSet, ComparisonOperator.IsNotSet],
            [AttributeDataType.Numeric] =
            [
                ComparisonOperator.Equals, ComparisonOperator.NotEquals, ComparisonOperator.GreaterThan,
                ComparisonOperator.GreaterThanOrEqual, ComparisonOperator.LessThan, ComparisonOperator.LessThanOrEqual,
                ComparisonOperator.IsSet, ComparisonOperator.IsNotSet
            ],
            [AttributeDataType.Date] =
            [
                ComparisonOperator.Equals, ComparisonOperator.NotEquals, ComparisonOperator.GreaterThan,
                ComparisonOperator.GreaterThanOrEqual, ComparisonOperator.LessThan, ComparisonOperator.LessThanOrEqual,
                ComparisonOperator.IsSet, ComparisonOperator.IsNotSet
            ],
            [AttributeDataType.Period] = [ComparisonOperator.IsSet, ComparisonOperator.IsNotSet],
            [AttributeDataType.Boolean] = [ComparisonOperator.IsTrue, ComparisonOperator.IsFalse, ComparisonOperator.IsSet, ComparisonOperator.IsNotSet],
            [AttributeDataType.OneOfMany] = [ComparisonOperator.Equals, ComparisonOperator.NotEquals, ComparisonOperator.IsSet, ComparisonOperator.IsNotSet]
        };

    public static string OperatorLabel(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equals => "=",
        ComparisonOperator.NotEquals => "≠",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => "≥",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "≤",
        ComparisonOperator.Contains => "contains",
        ComparisonOperator.StartsWith => "starts with",
        ComparisonOperator.IsTrue => "is checked",
        ComparisonOperator.IsFalse => "is not checked",
        ComparisonOperator.IsSet => "is filled in",
        ComparisonOperator.IsNotSet => "is not filled in",
        _ => op.ToString()
    };
}
