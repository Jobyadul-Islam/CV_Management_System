using CvManagement.Web.ViewModels.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CvManagement.Web.ViewComponents;

public class SelectableTableToolbarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string tableId, IReadOnlyList<ToolbarAction> actions)
    {
        ViewBag.TableId = tableId;
        return View(actions);
    }
}
