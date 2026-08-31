using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.Domain.Enums;

public enum AttributeDataType
{
    [Display(Name = "String (single-line text)")]
    String = 0,

    [Display(Name = "Text (Markdown)")]
    Text = 1,

    [Display(Name = "Image")]
    Image = 2,

    [Display(Name = "Numeric")]
    Numeric = 3,

    [Display(Name = "Date")]
    Date = 4,

    [Display(Name = "Period (date range)")]
    Period = 5,

    [Display(Name = "Boolean (checkbox)")]
    Boolean = 6,

    [Display(Name = "One of many (dropdown)")]
    OneOfMany = 7
}
