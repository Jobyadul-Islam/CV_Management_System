namespace CvManagement.Web.Domain;

public static class RoleNames
{
    public const string Candidate = "Candidate";
    public const string Recruiter = "Recruiter";
    public const string Administrator = "Administrator";

    public static readonly string[] All = [Candidate, Recruiter, Administrator];
}
