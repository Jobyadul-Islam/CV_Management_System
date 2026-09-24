using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.Services.Implementations;
using CvManagement.Web.ViewModels.Profile;
using Microsoft.Extensions.Options;

namespace CvManagement.Tests;

public class AutoSaveValidationTests
{
    private static readonly IUploadValidator Uploads = new UploadValidator(
        Options.Create(new ImageUploadOptions { CloudName = "demo-cloud", UnsignedUploadPreset = "p" }), TestLocalization.Localizer);

    private static string? Validate(AttributeDefinition attribute, AutoSaveChangeDto change, params int[] optionIds)
        => ProfileAutoSaveService.Validate(attribute, optionIds, change, Uploads, TestLocalization.Localizer);

    [Fact]
    public void String_longer_than_max_length_is_rejected()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.String, MaxLength = 5 };
        Assert.NotNull(Validate(attribute, new AutoSaveChangeDto { ValueString = "toolong" }));
    }

    [Fact]
    public void Clearing_a_constrained_field_is_always_allowed()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.String, MinLength = 3, RegexPattern = "^[A-Z]+$" };
        Assert.Null(Validate(attribute, new AutoSaveChangeDto { ValueString = "" }));
    }

    [Fact]
    public void String_not_matching_regex_is_rejected()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.String, RegexPattern = "^[A-Z]{2}$" };
        Assert.NotNull(Validate(attribute, new AutoSaveChangeDto { ValueString = "abc" }));
    }

    [Fact]
    public void Catastrophic_regex_times_out_into_an_error_instead_of_hanging()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.String, RegexPattern = "^(a+)+$" };
        var evil = new string('a', 40) + "!";
        Assert.NotNull(Validate(attribute, new AutoSaveChangeDto { ValueString = evil }));
    }

    [Fact]
    public void Numeric_outside_range_is_rejected()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.Numeric, MinValue = 0, MaxValue = 4 };
        Assert.NotNull(Validate(attribute, new AutoSaveChangeDto { ValueNumeric = 4.5m }));
        Assert.Null(Validate(attribute, new AutoSaveChangeDto { ValueNumeric = 3.2m }));
    }

    [Fact]
    public void Dropdown_option_of_another_attribute_is_rejected()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.OneOfMany };
        Assert.NotNull(Validate(attribute, new AutoSaveChangeDto { ValueOptionId = 99 }, 1, 2, 3));
        Assert.Null(Validate(attribute, new AutoSaveChangeDto { ValueOptionId = 2 }, 1, 2, 3));
    }

    [Fact]
    public void Period_ending_before_it_starts_is_rejected()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.Period };
        var change = new AutoSaveChangeDto { ValuePeriodStart = new DateOnly(2024, 5, 1), ValuePeriodEnd = new DateOnly(2024, 1, 1) };
        Assert.NotNull(Validate(attribute, change));
    }

    [Fact]
    public void Image_url_goes_through_the_central_upload_validator()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.Image };
        Assert.NotNull(Validate(attribute, new AutoSaveChangeDto { ValueImageUrl = "https://evil.example.com/x.png" }));
    }
}
