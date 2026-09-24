using CvManagement.Web.Models.Enums;

namespace CvManagement.Web.ViewModels.Attribute;

public class AttributePickerItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<AttributePickerOptionViewModel> Options { get; set; } = [];
}

public class AttributePickerOptionViewModel
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}
