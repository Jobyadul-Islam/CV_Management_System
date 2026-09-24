using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// Pure function evaluating one PositionAccessRule against a candidate's (possibly absent)
/// UserAttributeValue for that rule's attribute. No DB access -- callers batch-load values first.
/// </summary>
public static class RuleEvaluator
{
    public static bool Evaluate(PositionAccessRule rule, UserAttributeValue? value)
    {
        var isSet = value is not null && !AttributeEmptiness.IsEmpty(value, rule.Attribute.DataType);

        return rule.Operator switch
        {
            ComparisonOperator.IsSet => isSet,
            ComparisonOperator.IsNotSet => !isSet,
            _ when !isSet => false, // every other operator needs an actual value to compare
            _ => rule.Attribute.DataType switch
            {
                AttributeDataType.String => EvaluateString(rule, value!.ValueString!),
                AttributeDataType.Text => EvaluateText(rule, value!.ValueText!),
                AttributeDataType.Numeric => EvaluateNumeric(rule, value!.ValueNumeric!.Value),
                AttributeDataType.Date => EvaluateDate(rule, value!.ValueDate!.Value),
                AttributeDataType.Boolean => EvaluateBoolean(rule, value!.ValueBoolean!.Value),
                AttributeDataType.OneOfMany => EvaluateOption(rule, value!.ValueOptionId),
                _ => false // Image, Period: only IsSet/IsNotSet are meaningful, handled above
            }
        };
    }

    private static bool EvaluateString(PositionAccessRule rule, string actual)
    {
        var expected = rule.ComparisonValueString ?? string.Empty;
        return rule.Operator switch
        {
            ComparisonOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.StartsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool EvaluateText(PositionAccessRule rule, string actual) =>
        rule.Operator == ComparisonOperator.Contains
            && actual.Contains(rule.ComparisonValueString ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static bool EvaluateNumeric(PositionAccessRule rule, decimal actual)
    {
        var expected = rule.ComparisonValueNumeric ?? 0m;
        return rule.Operator switch
        {
            ComparisonOperator.Equals => actual == expected,
            ComparisonOperator.NotEquals => actual != expected,
            ComparisonOperator.GreaterThan => actual > expected,
            ComparisonOperator.GreaterThanOrEqual => actual >= expected,
            ComparisonOperator.LessThan => actual < expected,
            ComparisonOperator.LessThanOrEqual => actual <= expected,
            _ => false
        };
    }

    private static bool EvaluateDate(PositionAccessRule rule, DateOnly actual)
    {
        var expected = rule.ComparisonValueDate ?? DateOnly.MinValue;
        return rule.Operator switch
        {
            ComparisonOperator.Equals => actual == expected,
            ComparisonOperator.NotEquals => actual != expected,
            ComparisonOperator.GreaterThan => actual > expected,
            ComparisonOperator.GreaterThanOrEqual => actual >= expected,
            ComparisonOperator.LessThan => actual < expected,
            ComparisonOperator.LessThanOrEqual => actual <= expected,
            _ => false
        };
    }

    private static bool EvaluateBoolean(PositionAccessRule rule, bool actual) =>
        rule.Operator switch
        {
            ComparisonOperator.IsTrue => actual,
            ComparisonOperator.IsFalse => !actual,
            _ => false
        };

    private static bool EvaluateOption(PositionAccessRule rule, int? actualOptionId)
    {
        return rule.Operator switch
        {
            ComparisonOperator.Equals => actualOptionId == rule.ComparisonOptionId,
            ComparisonOperator.NotEquals => actualOptionId != rule.ComparisonOptionId,
            _ => false
        };
    }
}

/// <summary>Type-aware "is this value actually filled in" check, shared by rule evaluation and CV rendering.</summary>
public static class AttributeEmptiness
{
    public static bool IsEmpty(UserAttributeValue value, AttributeDataType dataType) => dataType switch
    {
        AttributeDataType.String => string.IsNullOrWhiteSpace(value.ValueString),
        AttributeDataType.Text => string.IsNullOrWhiteSpace(value.ValueText),
        AttributeDataType.Image => string.IsNullOrWhiteSpace(value.ValueImageUrl),
        AttributeDataType.Numeric => value.ValueNumeric is null,
        AttributeDataType.Date => value.ValueDate is null,
        AttributeDataType.Period => value.ValuePeriodStart is null,
        AttributeDataType.Boolean => value.ValueBoolean is null, // null = unanswered; false is a real answer
        AttributeDataType.OneOfMany => value.ValueOptionId is null,
        _ => true
    };
}
