using Microsoft.AspNetCore.Identity;

namespace CvManagement.Web.Domain;

/// <summary>
/// Identity user. First/Last name, location, photo, etc. live in <see cref="UserAttributeValue"/>
/// (the "Me" section attributes) so they go through the same engine as every other attribute.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Denormalized read cache of "FirstName LastName", kept in sync whenever those UserAttributeValue
    /// rows are saved. Not a second source of truth -- exists only so list pages (Admin Users, CV lists,
    /// discussion authors) don't need to join into the EAV table just to show a name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserAttributeValue> AttributeValues { get; set; } = new List<UserAttributeValue>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Cv> Cvs { get; set; } = new List<Cv>();
}
