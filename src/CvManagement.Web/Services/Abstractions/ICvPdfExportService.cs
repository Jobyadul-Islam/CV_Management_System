using CvManagement.Web.ViewModels.Cv;

namespace CvManagement.Web.Services.Abstractions;

public interface ICvPdfExportService
{
    /// <summary>Renders an already-built CV (see ICvRenderService) as a printable PDF. qrTargetUrl is
    /// the absolute URL the embedded QR code should link back to (the CV's own Details page).</summary>
    byte[] Generate(CvDetailsViewModel cv, string qrTargetUrl);
}
