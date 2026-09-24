using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.Services.Implementations;
using Microsoft.Extensions.Options;

namespace CvManagement.Tests;

public class UploadValidatorTests
{
    private static UploadValidator Validator(string cloudName = "demo-cloud", string preset = "unsigned") =>
        new(Options.Create(new ImageUploadOptions { CloudName = cloudName, UnsignedUploadPreset = preset }), TestLocalization.Localizer);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_url_is_allowed_because_it_clears_the_image(string? url)
        => Assert.Null(Validator().ValidateImageUrl(url));

    [Fact]
    public void Url_from_own_cloudinary_account_with_allowed_format_passes()
        => Assert.Null(Validator().ValidateImageUrl("https://res.cloudinary.com/demo-cloud/image/upload/v1/avatar.png"));

    [Fact]
    public void Plain_http_is_rejected()
        => Assert.NotNull(Validator().ValidateImageUrl("http://res.cloudinary.com/demo-cloud/image/upload/v1/avatar.png"));

    [Fact]
    public void Foreign_host_is_rejected()
        => Assert.NotNull(Validator().ValidateImageUrl("https://evil.example.com/demo-cloud/image/upload/avatar.png"));

    [Fact]
    public void Another_cloudinary_account_is_rejected()
        => Assert.NotNull(Validator().ValidateImageUrl("https://res.cloudinary.com/someone-else/image/upload/v1/avatar.png"));

    [Fact]
    public void Disallowed_file_format_is_rejected()
        => Assert.NotNull(Validator().ValidateImageUrl("https://res.cloudinary.com/demo-cloud/image/upload/v1/payload.svg"));

    [Fact]
    public void Any_url_is_rejected_when_cloudinary_is_not_configured()
        => Assert.NotNull(Validator(cloudName: "", preset: "").ValidateImageUrl("https://res.cloudinary.com/demo-cloud/image/upload/v1/avatar.png"));
}
