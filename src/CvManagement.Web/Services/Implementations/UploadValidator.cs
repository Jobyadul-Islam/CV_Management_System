using Microsoft.Extensions.Localization;
using CvManagement.Web.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace CvManagement.Web.Services.Implementations;

public class UploadValidator(IOptions<ImageUploadOptions> options, IStringLocalizer<SharedResource> localizer) : IUploadValidator
{
    public ImageUploadOptions Options { get; } = options.Value;

    public bool IsImageUploadConfigured =>
        !string.IsNullOrWhiteSpace(Options.CloudName) && !string.IsNullOrWhiteSpace(Options.UnsignedUploadPreset);

    public string? ValidateImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return localizer["Upload_MustBeHttps"];

        // Only URLs from this app's own Cloudinary account -- the widget always returns
        // https://res.cloudinary.com/<cloudName>/image/upload/...
        if (!IsImageUploadConfigured
            || !string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith($"/{Options.CloudName}/image/", StringComparison.OrdinalIgnoreCase))
            return localizer["Upload_MustUseButton"];

        var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
        if (!Options.AllowedFormats.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return localizer["Upload_AllowedFormats", string.Join(", ", Options.AllowedFormats)];

        return null;
    }
}
