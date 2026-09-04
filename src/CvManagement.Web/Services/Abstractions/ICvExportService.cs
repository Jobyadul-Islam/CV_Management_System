namespace CvManagement.Web.Services.Abstractions;

public interface ICvExportService
{
    /// <summary>
    /// Aggregate CSV of every CV created for a position, one row per candidate, one column per
    /// position-template attribute -- for analysis in Excel/Sheets. Null if the position doesn't
    /// exist. Visibility mirrors CvController: Recruiters see Published + currently-eligible CVs
    /// only; Administrators (isAdmin) see every CV regardless of status/eligibility.
    /// </summary>
    Task<string?> BuildCsvAsync(int positionId, bool isAdmin, CancellationToken ct = default);
}
