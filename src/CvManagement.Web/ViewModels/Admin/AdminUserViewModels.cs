namespace CvManagement.Web.ViewModels.Admin;

public class AdminUserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public bool IsBlocked { get; set; }
}

public class AdminUserEditRolesViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> SelectedRoles { get; set; } = [];
    public List<string> AllRoles { get; set; } = [];
}
