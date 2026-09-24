using CvManagement.Web.Models.Enums;

namespace CvManagement.Web.ViewModels.Attribute;

public class AttributeListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public int PositionUsageCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
