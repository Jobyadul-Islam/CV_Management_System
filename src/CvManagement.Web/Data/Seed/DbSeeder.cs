using CvManagement.Web.Domain;
using CvManagement.Web.Domain.Enums;
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

        await SeedRolesAsync(roleManager);
        var categories = await SeedCategoriesAsync(db);
        var builtIns = await SeedBuiltInAttributesAsync(db, categories);
        await SeedLibraryAttributesAsync(db, categories);
        await SeedDemoUsersAsync(userManager, db, builtIns);
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
        if (await db.Attributes.AnyAsync(a => !a.IsBuiltIn))
        {
            return;
        }

        var englishLevel = new AttributeDefinition
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
        };

        var presentationSkills = new AttributeDefinition
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
        };

        db.Attributes.AddRange(
            englishLevel,
            presentationSkills,
            new AttributeDefinition
            {
                Name = "GPA",
                Description = "Grade point average (0-4 scale).",
                DataType = AttributeDataType.Numeric,
                CategoryId = categories["Domain Knowledge"].Id
            },
            new AttributeDefinition
            {
                Name = "IELTS Score",
                Description = "IELTS band score.",
                DataType = AttributeDataType.Numeric,
                CategoryId = categories["Certification"].Id
            },
            new AttributeDefinition
            {
                Name = "Remote Work Availability",
                Description = "Willing and able to work remotely.",
                DataType = AttributeDataType.Boolean,
                CategoryId = categories["Soft Skills"].Id
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoUsersAsync(
        UserManager<ApplicationUser> userManager, ApplicationDbContext db,
        Dictionary<string, AttributeDefinition> builtIns)
    {
        await CreateDemoUserAsync(userManager, db, builtIns,
            email: "admin@demo.local", role: RoleNames.Administrator,
            firstName: "Ada", lastName: "Admin", location: "Warsaw, Poland");

        await CreateDemoUserAsync(userManager, db, builtIns,
            email: "recruiter@demo.local", role: RoleNames.Recruiter,
            firstName: "Rita", lastName: "Recruiter", location: "Berlin, Germany");

        await CreateDemoUserAsync(userManager, db, builtIns,
            email: "candidate@demo.local", role: RoleNames.Candidate,
            firstName: "Cameron", lastName: "Candidate", location: "Kyiv, Ukraine");
    }

    private static async Task CreateDemoUserAsync(
        UserManager<ApplicationUser> userManager, ApplicationDbContext db,
        Dictionary<string, AttributeDefinition> builtIns,
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

        db.UserAttributeValues.AddRange(
            new UserAttributeValue { UserId = user.Id, AttributeId = builtIns["First Name"].Id, ValueString = firstName },
            new UserAttributeValue { UserId = user.Id, AttributeId = builtIns["Last Name"].Id, ValueString = lastName },
            new UserAttributeValue { UserId = user.Id, AttributeId = builtIns["Location"].Id, ValueString = location },
            new UserAttributeValue { UserId = user.Id, AttributeId = builtIns["Personal Photo"].Id });

        await db.SaveChangesAsync();
    }
}
