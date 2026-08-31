namespace CvManagement.Web.ViewModels.Shared;

/// <summary>
/// One button in a SelectableTableToolbar. "Get" mode navigates to Url with "{id}" replaced by the
/// single selected row's id (MinSelected=MaxSelected=1). "Post" mode submits the selected ids as a
/// bulk form POST to Url. See wwwroot/js/table-toolbar.js for the client-side wiring.
/// </summary>
public record ToolbarAction(
    string Label,
    string Url,
    string Mode, // "get" | "post"
    int MinSelected = 1,
    int? MaxSelected = null,
    string? Confirm = null,
    string CssClass = "btn-outline-secondary");
