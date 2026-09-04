using System.Globalization;
using CvManagement.Web.Data;
using CvManagement.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Locally-registered accounts must confirm their email before signing in (the optional
        // "form auth with email confirmation" requirement). OAuth sign-ins and seeded demo accounts
        // are created with EmailConfirmed = true, so this only affects the password-registration path.
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var authBuilder = builder.Services.AddAuthentication();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
if (!string.IsNullOrEmpty(githubClientId) && !string.IsNullOrEmpty(githubClientSecret))
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
    });
}

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddSignalR();

builder.Services.AddAuthorization();

// Auto-save posts JSON (not a form), so the antiforgery token travels as a request header --
// IAntiforgery only reads the form field automatically for form-urlencoded/multipart bodies.
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IUserOnboardingService,
    CvManagement.Web.Services.Implementations.UserOnboardingService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IAttributeService,
    CvManagement.Web.Services.Implementations.AttributeService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IPositionAccessEvaluator,
    CvManagement.Web.Services.Implementations.PositionAccessEvaluator>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IPositionService,
    CvManagement.Web.Services.Implementations.PositionService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.ITagService,
    CvManagement.Web.Services.Implementations.TagService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IProfileService,
    CvManagement.Web.Services.Implementations.ProfileService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IProfileAutoSaveService,
    CvManagement.Web.Services.Implementations.ProfileAutoSaveService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IProjectService,
    CvManagement.Web.Services.Implementations.ProjectService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.ICvRenderService,
    CvManagement.Web.Services.Implementations.CvRenderService>();
builder.Services.AddSingleton<CvManagement.Web.Services.Abstractions.IMarkdownRenderer,
    CvManagement.Web.Services.Implementations.MarkdownRenderer>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IDiscussionService,
    CvManagement.Web.Services.Implementations.DiscussionService>();
builder.Services.AddSingleton<CvManagement.Web.Services.Abstractions.ISearchIndexService,
    CvManagement.Web.Services.Implementations.SearchIndexService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.ISearchService,
    CvManagement.Web.Services.Implementations.SearchService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IAdminUserService,
    CvManagement.Web.Services.Implementations.AdminUserService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IAppEmailSender,
    CvManagement.Web.Services.Implementations.SmtpEmailSender>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.ICvPdfExportService,
    CvManagement.Web.Services.Implementations.CvPdfExportService>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.IBadgeService,
    CvManagement.Web.Services.Implementations.BadgeService>();
builder.Services.AddSingleton<CvManagement.Web.Services.Abstractions.IBadgeSvgRenderer,
    CvManagement.Web.Services.Implementations.BadgeSvgRenderer>();
builder.Services.AddScoped<CvManagement.Web.Services.Abstractions.ICvExportService,
    CvManagement.Web.Services.Implementations.CvExportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await CvManagement.Web.Data.Seed.DbSeeder.SeedAsync(scope.ServiceProvider);

    var searchIndex = scope.ServiceProvider.GetRequiredService<CvManagement.Web.Services.Abstractions.ISearchIndexService>();
    await searchIndex.RebuildAllAsync();
}

var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ru") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = [new CookieRequestCultureProvider()]
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<CvManagement.Web.Hubs.DiscussionHub>("/hubs/discussion");

app.Run();
