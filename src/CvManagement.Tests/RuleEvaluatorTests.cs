using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Implementations;

namespace CvManagement.Tests;

public class RuleEvaluatorTests
{
    private static PositionAccessRule Rule(AttributeDataType type, ComparisonOperator op,
        string? str = null, decimal? num = null, DateOnly? date = null, int? optionId = null) => new()
    {
        Attribute = new AttributeDefinition { DataType = type },
        Operator = op,
        ComparisonValueString = str,
        ComparisonValueNumeric = num,
        ComparisonValueDate = date,
        ComparisonOptionId = optionId
    };

    [Fact]
    public void Numeric_GreaterThan_passes_when_value_exceeds_threshold()
    {
        var rule = Rule(AttributeDataType.Numeric, ComparisonOperator.GreaterThan, num: 7.0m);
        var value = new UserAttributeValue { ValueNumeric = 7.5m };

        Assert.True(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void Numeric_GreaterThan_fails_when_value_equals_threshold()
    {
        var rule = Rule(AttributeDataType.Numeric, ComparisonOperator.GreaterThan, num: 7.0m);
        var value = new UserAttributeValue { ValueNumeric = 7.0m };

        Assert.False(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void Boolean_IsTrue_fails_when_unset()
    {
        var rule = Rule(AttributeDataType.Boolean, ComparisonOperator.IsTrue);
        var value = new UserAttributeValue { ValueBoolean = null };

        Assert.False(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void Boolean_IsTrue_fails_when_explicitly_false()
    {
        var rule = Rule(AttributeDataType.Boolean, ComparisonOperator.IsTrue);
        var value = new UserAttributeValue { ValueBoolean = false };

        Assert.False(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void Boolean_IsTrue_passes_when_true()
    {
        var rule = Rule(AttributeDataType.Boolean, ComparisonOperator.IsTrue);
        var value = new UserAttributeValue { ValueBoolean = true };

        Assert.True(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void OneOfMany_Equals_compares_by_option_id()
    {
        var rule = Rule(AttributeDataType.OneOfMany, ComparisonOperator.Equals, optionId: 42);

        Assert.True(RuleEvaluator.Evaluate(rule, new UserAttributeValue { ValueOptionId = 42 }));
        Assert.False(RuleEvaluator.Evaluate(rule, new UserAttributeValue { ValueOptionId = 43 }));
    }

    [Fact]
    public void Any_operator_fails_when_no_value_row_exists()
    {
        var rule = Rule(AttributeDataType.Numeric, ComparisonOperator.GreaterThan, num: 7.0m);

        Assert.False(RuleEvaluator.Evaluate(rule, null));
    }

    [Fact]
    public void IsNotSet_passes_when_no_value_row_exists()
    {
        var rule = Rule(AttributeDataType.Numeric, ComparisonOperator.IsNotSet);

        Assert.True(RuleEvaluator.Evaluate(rule, null));
    }

    [Fact]
    public void IsSet_fails_for_a_row_that_exists_but_is_empty()
    {
        var rule = Rule(AttributeDataType.String, ComparisonOperator.IsSet);
        var value = new UserAttributeValue { ValueString = "   " };

        Assert.False(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void String_Contains_is_case_insensitive()
    {
        var rule = Rule(AttributeDataType.String, ComparisonOperator.Contains, str: "senior");
        var value = new UserAttributeValue { ValueString = "Senior Backend Engineer" };

        Assert.True(RuleEvaluator.Evaluate(rule, value));
    }

    [Fact]
    public void Date_LessThanOrEqual_boundary_is_inclusive()
    {
        var boundary = new DateOnly(2026, 1, 1);
        var rule = Rule(AttributeDataType.Date, ComparisonOperator.LessThanOrEqual, date: boundary);
        var value = new UserAttributeValue { ValueDate = boundary };

        Assert.True(RuleEvaluator.Evaluate(rule, value));
    }
}
