using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.Controllers;

/// <summary>
/// Backs the header language switcher on every page. The choice is stored in the standard ASP.NET Core
/// culture cookie for a year, and RequestLocalization (Program.cs) reads it on every request, so the
/// whole UI -- views, validation, status messages, exports -- follows it until changed again.
/// </summary>
public class CultureController : Controller
{
    private static readonly string[] SupportedCultures = ["en", "ru"];

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetCulture(string culture, string returnUrl)
    {
        if (!SupportedCultures.Contains(culture))
        {
            culture = SupportedCultures[0];
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
