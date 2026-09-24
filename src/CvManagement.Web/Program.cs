using System.Globalization;
using CvManagement.Web.Data;
using CvManagement.Web.Models;
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
    .AddDefaultTokenProviders()
    // Identity's own messages ("Passwords must have...", "Email is already taken") in the UI language.
    .AddErrorDescriber<CvManagement.Web.Services.Implementations.LocalizedIdentityErrorDescriber>();

// Re-check each auth cookie's security stamp every minute (default 30) so an Administrator's
// block or role change reaches sessions that are already open almost immediately.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

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
        // Without this scope GitHub only returns the *public* profile email, which most users keep
        // private -- and account creation/linking needs an email. With it, the handler reads the
        // primary verified address from GitHub's /user/emails endpoint.
        options.Scope.Add("user:email");
    });
}

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    // One shared resource file for everything: [Display] names, enum labels and validation
    // ErrorMessage keys all resolve from SharedResource.{culture}.resx (see ViewModels/ValidationMessages).
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(CvManagement.Web.SharedResource)));

// Model-binding errors (e.g. "The value 'abc' is not valid") are produced by MVC itself, not by
// DataAnnotations, so they're localized separately. The localizer resolves the culture at call time.
builder.Services.AddOptions<Microsoft.AspNetCore.Mvc.MvcOptions>()
    .Configure<Microsoft.Extensions.Localization.IStringLocalizerFactory>((options, factory) =>
    {
        var localizer = factory.Create(typeof(CvManagement.Web.SharedResource));
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustNotBeNullAccessor(value => localizer["Binding_ValueRequired", value]);
        messages.SetMissingBindRequiredValueAccessor(name => localizer["Binding_MissingValue", name]);
        messages.SetMissingKeyOrValueAccessor(() => localizer["Binding_KeyOrValueRequired"]);
        messages.SetMissingRequestBodyRequiredValueAccessor(() => localizer["Binding_BodyRequired"]);
        messages.SetAttemptedValueIsInvalidAccessor((value, name) => localizer["Binding_InvalidValueFor", value, name]);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => localizer["Binding_InvalidValue", value]);
        messages.SetUnknownValueIsInvalidAccessor(name => localizer["Binding_UnknownValueFor", name]);
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => localizer["Binding_UnknownValue"]);
        messages.SetValueIsInvalidAccessor(value => localizer["Binding_InvalidValue", value]);
        messages.SetValueMustBeANumberAccessor(name => localizer["Binding_MustBeNumber", name]);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => localizer["Binding_MustBeNumberGeneric"]);
    });

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

// Centralized upload rules: one options object feeds both the Cloudinary widget and server-side URL
// validation. The older top-level "Cloudinary" keys (documented in the README) still work.
builder.Services.Configure<CvManagement.Web.Services.Abstractions.ImageUploadOptions>(
    builder.Configuration.GetSection(CvManagement.Web.Services.Abstractions.ImageUploadOptions.SectionName));
builder.Services.PostConfigure<CvManagement.Web.Services.Abstractions.ImageUploadOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.CloudName)) options.CloudName = builder.Configuration["Cloudinary:CloudName"] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(options.UnsignedUploadPreset)) options.UnsignedUploadPreset = builder.Configuration["Cloudinary:UnsignedUploadPreset"] ?? string.Empty;
});
builder.Services.AddSingleton<CvManagement.Web.Services.Abstractions.IUploadValidator,
    CvManagement.Web.Services.Implementations.UploadValidator>();

builder.Services.Configure<CvManagement.Web.Services.Implementations.DraftCvReminderOptions>(
    builder.Configuration.GetSection(CvManagement.Web.Services.Implementations.DraftCvReminderOptions.SectionName));
builder.Services.AddHostedService<CvManagement.Web.Services.Implementations.DraftCvReminderService>();

// Hosted behind a reverse proxy (Azure App Service, Render, ...), TLS ends at the proxy and the app
// sees plain http. Honouring X-Forwarded-Proto/For restores the real scheme and client IP, so HTTPS
// redirection and the Google/GitHub OAuth redirect URIs use https://<your-domain>. The proxy's address
// isn't known in advance, hence the cleared lists; the platform only lets traffic in through it.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                               | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

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
