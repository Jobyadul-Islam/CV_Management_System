namespace CvManagement.Web.Services.Abstractions;

/// <summary>Renders user-authored Markdown (Text attributes, project descriptions, discussion posts) to sanitized HTML.</summary>
public interface IMarkdownRenderer
{
    string ToSafeHtml(string? markdown);
}
