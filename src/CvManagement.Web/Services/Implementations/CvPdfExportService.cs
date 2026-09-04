using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using CvManagement.Web.ViewModels.Cv;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CvManagement.Web.Services.Implementations;

public class CvPdfExportService : ICvPdfExportService
{
    public byte[] Generate(CvDetailsViewModel cv, string qrTargetUrl)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(qrTargetUrl, QRCodeGenerator.ECCLevel.Q);
        var qrPng = new PngByteQRCode(qrData).GetGraphic(10);

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(cv.CandidateDisplayName).FontSize(22).SemiBold();
                        column.Item().Text(text =>
                        {
                            text.Span(cv.PositionTitle).FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2);
                            if (!string.IsNullOrWhiteSpace(cv.Company))
                            {
                                text.Span($"  ·  {cv.Company}").FontSize(11);
                            }
                            if (cv.Level is not null)
                            {
                                text.Span($"  ·  {cv.Level}").FontSize(11);
                            }
                        });
                        column.Item().PaddingTop(2).Text(cv.Status == CvStatus.Published ? "Published CV" : "Draft CV")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(80).Column(column =>
                    {
                        column.Item().Image(qrPng);
                        column.Item().AlignCenter().Text("Scan to view online").FontSize(7).FontColor(Colors.Grey.Medium);
                    });
                });

                page.Content().PaddingTop(15).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(c => ComposeFields(c, cv));

                    if (cv.Projects.Count > 0)
                    {
                        column.Item().PaddingTop(5).Text("Projects").FontSize(13).SemiBold();
                        foreach (var project in cv.Projects)
                        {
                            column.Item().Element(c => ComposeProject(c, project));
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeFields(IContainer container, CvDetailsViewModel cv)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            foreach (var field in cv.Fields)
            {
                var value = AttributeValueFormatter.ToPlainText(field);
                column.Item().Row(row =>
                {
                    row.ConstantItem(150).Text(field.AttributeName).SemiBold();
                    row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "(not provided)" : value)
                        .FontColor(string.IsNullOrWhiteSpace(value) ? Colors.Red.Medium : Colors.Black);
                });
            }
        });
    }

    private static void ComposeProject(IContainer container, ViewModels.Profile.ProjectListItemViewModel project)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(text =>
            {
                text.Span(project.Name).SemiBold();
                var period = project.PeriodEnd is null
                    ? $"  ({project.PeriodStart:yyyy-MM} - present)"
                    : $"  ({project.PeriodStart:yyyy-MM} - {project.PeriodEnd:yyyy-MM})";
                text.Span(period).FontSize(9).FontColor(Colors.Grey.Medium);
            });
            if (project.Tags.Count > 0)
            {
                column.Item().Text(string.Join(", ", project.Tags)).FontSize(9).FontColor(Colors.Blue.Darken1);
            }
            if (!string.IsNullOrWhiteSpace(project.DescriptionMarkdown))
            {
                column.Item().Text(project.DescriptionMarkdown).FontSize(9);
            }
        });
    }
}
