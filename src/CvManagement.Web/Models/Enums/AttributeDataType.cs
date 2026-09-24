using System.ComponentModel.DataAnnotations;

namespace CvManagement.Web.Models.Enums;

public enum AttributeDataType
{
    [Display(Name = "DataType_String")]
    String = 0,

    [Display(Name = "DataType_Text")]
    Text = 1,

    [Display(Name = "DataType_Image")]
    Image = 2,

    [Display(Name = "DataType_Numeric")]
    Numeric = 3,

    [Display(Name = "DataType_Date")]
    Date = 4,

    [Display(Name = "DataType_Period")]
    Period = 5,

    [Display(Name = "DataType_Boolean")]
    Boolean = 6,

    [Display(Name = "DataType_OneOfMany")]
    OneOfMany = 7
}
