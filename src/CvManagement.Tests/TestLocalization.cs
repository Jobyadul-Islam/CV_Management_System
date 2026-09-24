using System.Globalization;
using CvManagement.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CvManagement.Tests;

/// <summary>
/// The app's real resource-backed localizer (SharedResource.en/ru.resx), so tests exercise the same
/// strings users see -- and can assert on the Russian ones by switching the UI culture.
/// </summary>
public static class TestLocalization
{
    public static readonly IStringLocalizer<SharedResource> Localizer = new StringLocalizer<SharedResource>(
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance));

    /// <summary>Runs <paramref name="action"/> with the given UI culture, restoring the previous one afterwards.</summary>
    public static void InCulture(string culture, Action action)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            action();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }
}
