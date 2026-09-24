using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.Models.Enums;

// Display names are SharedResource keys (localized via DataAnnotations localization).
public enum CvStatus
{
    [Display(Name = "Cv_Draft")]
    Draft = 0,

    [Display(Name = "Cv_Published")]
    Published = 1
}
