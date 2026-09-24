using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.Models.Enums;

// Display names are SharedResource keys (localized via DataAnnotations localization).
public enum PositionLevel
{
    [Display(Name = "Level_Junior")]
    Junior = 0,

    [Display(Name = "Level_Middle")]
    Middle = 1,

    [Display(Name = "Level_Senior")]
    Senior = 2,

    [Display(Name = "Level_CLevel")]
    CLevel = 3
}
