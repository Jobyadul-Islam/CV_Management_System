using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Implementations;
using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Tests;

/// <summary>
/// Choosing Russian must switch the whole UI, not just the views -- these cover the server-generated
/// text paths (service validation, Identity errors, exports) against the real resource files.
/// </summary>
public class LocalizationTests
{
    [Fact]
    public void Service_validation_message_follows_the_ui_culture()
    {
        var attribute = new AttributeDefinition { DataType = AttributeDataType.String, MaxLength = 3 };
        var change = new AutoSaveChangeDto { ValueString = "toolong" };
        var uploads = new UploadValidator(Microsoft.Extensions.Options.Options.Create(
            new Web.Services.Abstractions.ImageUploadOptions()), TestLocalization.Localizer);

        string? english = null, russian = null;
        TestLocalization.InCulture("en", () => english = ProfileAutoSaveService.Validate(attribute, [], change, uploads, TestLocalization.Localizer));
        TestLocalization.InCulture("ru", () => russian = ProfileAutoSaveService.Validate(attribute, [], change, uploads, TestLocalization.Localizer));

        Assert.Equal("Must be at most 3 characters.", english);
        Assert.Equal("Не более 3 символов.", russian);
    }

    [Fact]
    public void Identity_password_error_is_translated()
    {
        var describer = new LocalizedIdentityErrorDescriber(TestLocalization.Localizer);
        string? russian = null;
        TestLocalization.InCulture("ru", () => russian = describer.PasswordTooShort(8).Description);

        Assert.Equal("Пароль должен содержать не менее 8 символов.", russian);
    }

    [Fact]
    public void Exported_yes_no_values_follow_the_ui_culture()
    {
        var field = new AttributeValueViewModel { DataType = AttributeDataType.Boolean, ValueBoolean = true };
        string? russian = null;
        TestLocalization.InCulture("ru", () => russian = AttributeValueFormatter.ToPlainText(field, TestLocalization.Localizer));

        Assert.Equal("Да", russian);
    }

    [Fact]
    public void Enum_display_names_are_resource_keys_with_translations()
    {
        // [Display(Name = ...)] on enums holds a SharedResource key; DataAnnotations localization resolves it.
        var key = typeof(PositionLevel).GetField(nameof(PositionLevel.Senior))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>().Single().Name!;

        string? english = null, russian = null;
        TestLocalization.InCulture("en", () => english = TestLocalization.Localizer[key]);
        TestLocalization.InCulture("ru", () => russian = TestLocalization.Localizer["Role_Recruiter"]);

        Assert.Equal("Senior", english);
        Assert.Equal("Рекрутер", russian);
    }
}
