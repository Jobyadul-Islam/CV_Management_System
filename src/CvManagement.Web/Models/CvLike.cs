namespace CvManagement.Web.Models;

/// <summary>
/// Recruiter-only like on a CV. Insert/delete only -- no field-level edit ever happens, so no
/// RowVersion; the (CvId, RecruiterUserId) unique constraint is itself the concurrency guard.
/// </summary>
public class CvLike
{
    public int Id { get; set; }

    public int CvId { get; set; }
    public Cv Cv { get; set; } = null!;

    public string RecruiterUserId { get; set; } = string.Empty;
    public ApplicationUser RecruiterUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
