namespace CvManagement.Web.Models;

/// <summary>
/// Stable identifiers for the Me-tab built-in attributes. Code looks built-ins up by
/// <see cref="AttributeDefinition.SystemKey"/>, never by display name -- Recruiters may rename
/// "First Name" to anything, and display-name sync, OAuth name prefill and seeding must keep working.
/// </summary>
public static class BuiltInAttributeKeys
{
    public const string FirstName = "FirstName";
    public const string LastName = "LastName";
    public const string Location = "Location";
    public const string PersonalPhoto = "PersonalPhoto";
}
