using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.Models.Enums;

// Display names are SharedResource keys (localized via DataAnnotations localization).
public enum PositionAccessMode
{
    [Display(Name = "Positions_Public")]
    Public = 0,

    [Display(Name = "Positions_Restricted")]
    Restricted = 1
}
