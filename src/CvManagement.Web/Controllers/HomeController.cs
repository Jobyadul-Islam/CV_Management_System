using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CvManagement.Web.Models;

namespace CvManagement.Web.Controllers;

public class HomeController : Controller
{
    // Phase 8 replaces this with the real Main Page (latest/popular positions, tag cloud, stats).
    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
