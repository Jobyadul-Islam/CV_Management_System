using CvManagement.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using CvManagement.Web.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.ViewComponents;

public class AttributePickerViewComponent(IAttributeService attributes, UserManager<ApplicationUser> userManager) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string modalId, IReadOnlyList<int>? excludeIds = null)
    {
        var userId = userManager.GetUserId(UserClaimsPrincipal)!;
        var categories = await attributes.GetCategoriesAsync();
        var recent = await attributes.GetRecentlyUsedAsync(userId, excludeIds, take: 8);
        var all = await attributes.SearchForPickerAsync(null, null, excludeIds);

        ViewBag.ModalId = modalId;
        ViewBag.Categories = categories;
        ViewBag.Recent = recent;
        return View(all);
    }
}
