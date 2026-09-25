namespace CvManagement.Web.Services.Abstractions;

/// <summary>
/// Single source of truth for image-upload rules. Files never reach this server (the spec forbids it):
/// the browser uploads straight to Cloudinary, so the same <see cref="ImageUploadOptions"/> both
/// configure the upload widget's client-side limits and gate the resulting URL server-side before it
/// is stored -- a hand-crafted autosave request can't point a profile photo at an arbitrary host.
/// </summary>
public interface IUploadValidator
{
    ImageUploadOptions Options { get; }

    /// <summary>True when Cloudinary is configured, i.e. the upload widget can be shown.</summary>
    bool IsImageUploadConfigured { get; }

    /// <summary>Null when <paramref name="url"/> is acceptable (an empty value clears the image); otherwise an error message.</summary>
    string? ValidateImageUrl(string? url);
}

public class ImageUploadOptions
{
    public const string SectionName = "Uploads:Images";

    public string CloudName { get; set; } = string.Empty;
    public string UnsignedUploadPreset { get; set; } = string.Empty;
    public static readonly string[] DefaultAllowedFormats = ["jpg", "jpeg", "png", "webp", "gif"];

    // Empty here on purpose: the configuration binder *appends* configured array items to an initialized
    // array, which listed every format twice. Program.cs falls back to DefaultAllowedFormats when unset.
    public string[] AllowedFormats { get; set; } = [];
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}
