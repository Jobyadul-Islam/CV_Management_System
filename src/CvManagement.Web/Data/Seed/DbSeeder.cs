using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
using CvManagement.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Web.Data.Seed;

/// <summary>
/// Idempotent startup seed: roles, attribute categories, the four Me-tab built-in attributes,
/// a handful of library attributes used throughout the spec's own examples, and one demo account
/// per role so every role-gated flow is testable without live OAuth credentials.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Demo@12345";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var onboarding = services.GetRequiredService<IUserOnboardingService>();

        await SeedRolesAsync(roleManager);
        var categories = await SeedCategoriesAsync(db);
        var builtIns = await SeedBuiltInAttributesAsync(db, categories);
        await SeedLibraryAttributesAsync(db, categories);
        await SeedTagsAsync(db);
        await SeedDemoUsersAsync(userManager, db, onboarding, builtIns);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task<Dictionary<string, AttributeCategory>> SeedCategoriesAsync(ApplicationDbContext db)
    {
        string[] names = ["Personal Information", "Certification", "Domain Knowledge", "Soft Skills"];
        var existing = await db.AttributeCategories.ToDictionaryAsync(c => c.Name);

        foreach (var name in names)
        {
            if (!existing.ContainsKey(name))
            {
                var category = new AttributeCategory { Name = name };
                db.AttributeCategories.Add(category);
                existing[name] = category;
            }
        }

        await db.SaveChangesAsync();
        return existing;
    }

    private static async Task<Dictionary<string, AttributeDefinition>> SeedBuiltInAttributesAsync(
        ApplicationDbContext db, Dictionary<string, AttributeCategory> categories)
    {
        var personalInfo = categories["Personal Information"];
        var existing = await db.Attributes.Where(a => a.IsBuiltIn).ToDictionaryAsync(a => a.Name);

        (string Name, string Description, AttributeDataType Type)[] builtIns =
        [
            ("First Name", "Candidate's given name.", AttributeDataType.String),
            ("Last Name", "Candidate's family name.", AttributeDataType.String),
            ("Location", "City and country of residence.", AttributeDataType.String),
            ("Personal Photo", "Profile photo.", AttributeDataType.Image)
        ];

        foreach (var (name, description, type) in builtIns)
        {
            if (!existing.ContainsKey(name))
            {
                var attribute = new AttributeDefinition
                {
                    Name = name,
                    Description = description,
                    DataType = type,
                    CategoryId = personalInfo.Id,
                    IsBuiltIn = true
                };
                db.Attributes.Add(attribute);
                existing[name] = attribute;
            }
        }

        await db.SaveChangesAsync();
        return existing;
    }

    private static async Task SeedLibraryAttributesAsync(
        ApplicationDbContext db, Dictionary<string, AttributeCategory> categories)
    {
        var existingNames = (await db.Attributes.Select(a => a.Name).ToListAsync()).ToHashSet();

        List<AttributeDefinition> candidates =
        [
            new()
            {
                Name = "English Level",
                Description = "Spoken and written English proficiency.",
                DataType = AttributeDataType.OneOfMany,
                CategoryId = categories["Domain Knowledge"].Id,
                Options =
                [
                    new AttributeOption { Label = "Beginner", SortOrder = 0 },
                    new AttributeOption { Label = "Intermediate", SortOrder = 1 },
                    new AttributeOption { Label = "Advanced", SortOrder = 2 },
                    new AttributeOption { Label = "Fluent", SortOrder = 3 }
                ]
            },
            new()
            {
                Name = "Presentation Skills",
                Description = "Ability to present to an audience.",
                DataType = AttributeDataType.OneOfMany,
                CategoryId = categories["Soft Skills"].Id,
                Options =
                [
                    new AttributeOption { Label = "Beginner", SortOrder = 0 },
                    new AttributeOption { Label = "Intermediate", SortOrder = 1 },
                    new AttributeOption { Label = "Advanced", SortOrder = 2 }
                ]
            },
            new()
            {
                Name = "GPA",
                Description = "Grade point average (0-4 scale).",
                DataType = AttributeDataType.Numeric,
                CategoryId = categories["Domain Knowledge"].Id
            },
            new()
            {
                Name = "IELTS Score",
                Description = "IELTS band score.",
                DataType = AttributeDataType.Numeric,
                CategoryId = categories["Certification"].Id
            },
            new()
            {
                Name = "Remote Work Availability",
                Description = "Willing and able to work remotely.",
                DataType = AttributeDataType.Boolean,
                CategoryId = categories["Soft Skills"].Id
            },
            // From the spec's own worked example (CAP / Junior Data Engineer @ Acme Corp.):
            new()
            {
                Name = "CAP",
                Description = "Certified Analytics Professional exam level.",
                DataType = AttributeDataType.OneOfMany,
                CategoryId = categories["Certification"].Id,
                Options =
                [
                    new AttributeOption { Label = "None", SortOrder = 0 },
                    new AttributeOption { Label = "Essentials", SortOrder = 1 },
                    new AttributeOption { Label = "Pro", SortOrder = 2 },
                    new AttributeOption { Label = "Expert", SortOrder = 3 }
                ]
            },
            new()
            {
                Name = "Python",
                Description = "Proficient with Python.",
                DataType = AttributeDataType.Boolean,
                CategoryId = categories["Domain Knowledge"].Id
            },
            new()
            {
                Name = "Apache Hadoop",
                Description = "Proficient with Apache Hadoop.",
                DataType = AttributeDataType.Boolean,
                CategoryId = categories["Domain Knowledge"].Id
            }
        ];

        var toAdd = candidates.Where(a => !existingNames.Contains(a.Name)).ToList();
        if (toAdd.Count > 0)
        {
            db.Attributes.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedTagsAsync(ApplicationDbContext db)
    {
        // Technology tags from the spec's worked example ("SQL, R, and Python as filters..."), plus a
        // few common ones so Projects/Positions have realistic tag data to demo against.
        string[] names = ["SQL", "R", "Python", "Apache Hadoop", "Java", "JavaScript", "TypeScript", "C#", "Docker"];
        var existing = await db.Tags.Select(t => t.Name).ToListAsync();
        var toAdd = names.Where(n => !existing.Contains(n)).Select(n => new Tag { Name = n }).ToList();

        if (toAdd.Count > 0)
        {
            db.Tags.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedDemoUsersAsync(
        UserManager<ApplicationUser> userManager, ApplicationDbContext db,
        IUserOnboardingService onboarding, Dictionary<string, AttributeDefinition> builtIns)
    {
        await CreateDemoUserAsync(userManager, db, onboarding, builtIns,
            email: "admin@demo.local", role: RoleNames.Administrator,
            firstName: "Ada", lastName: "Admin", location: "Warsaw, Poland");

        await CreateDemoUserAsync(userManager, db, onboarding, builtIns,
            email: "recruiter@demo.local", role: RoleNames.Recruiter,
            firstName: "Rita", lastName: "Recruiter", location: "Berlin, Germany");

        await CreateDemoUserAsync(userManager, db, onboarding, builtIns,
            email: "candidate@demo.local", role: RoleNames.Candidate,
            firstName: "Cameron", lastName: "Candidate", location: "Kyiv, Ukraine");
    }

    private static async Task CreateDemoUserAsync(
        UserManager<ApplicationUser> userManager, ApplicationDbContext db,
        IUserOnboardingService onboarding, Dictionary<string, AttributeDefinition> builtIns,
        string email, string role, string firstName, string lastName, string location)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"{firstName} {lastName}"
        };

        var result = await userManager.CreateAsync(user, DbSeeder.DemoPassword);
        if (!result.Succeeded)
        {
            return;
        }

        await userManager.AddToRoleAsync(user, role);
        await onboarding.InitializeNewUserAsync(user);

        var values = await db.UserAttributeValues
            .Where(v => v.UserId == user.Id)
            .ToDictionaryAsync(v => v.AttributeId);

        values[builtIns["First Name"].Id].ValueString = firstName;
        values[builtIns["Last Name"].Id].ValueString = lastName;
        values[builtIns["Location"].Id].ValueString = location;

        await db.SaveChangesAsync();
    }
}
