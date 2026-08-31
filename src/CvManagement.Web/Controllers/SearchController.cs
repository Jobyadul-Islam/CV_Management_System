using CvManagement.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.Controllers;

/// <summary>Header search entry point, reachable from every page. Backed by the Lucene.NET index (see ISearchIndexService).</summary>
public class SearchController(ISearchService search) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Query"] = q;
        var results = await search.SearchAsync(q, User);
        return View(results);
    }
}
