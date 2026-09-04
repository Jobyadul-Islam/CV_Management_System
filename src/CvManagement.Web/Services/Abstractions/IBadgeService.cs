namespace CvManagement.Web.Services.Abstractions;

public record EarnedBadge(string Label, string Icon, string ColorHex);

public interface IBadgeService
{
    /// <summary>Achievement badges ("5 CVs", "25 Likes", ...) the candidate has earned so far, based
    /// on their current project/CV/like counts. Empty for a candidate who hasn't earned any yet.</summary>
    Task<IReadOnlyList<EarnedBadge>> GetEarnedBadgesAsync(string userId, CancellationToken ct = default);
}
