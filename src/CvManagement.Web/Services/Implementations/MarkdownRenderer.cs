using CvManagement.Web.Services.Abstractions;
using Ganss.Xss;
using Markdig;

namespace CvManagement.Web.Services.Implementations;

public class MarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml() // raw inline HTML in user Markdown is never trusted; sanitizer is a second layer
        .Build();

    private readonly HtmlSanitizer _sanitizer = new();

    public string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToHtml(markdown, Pipeline);
        return _sanitizer.Sanitize(html);
    }
}
