namespace CvManagement.Web.Models;

public class AttributeCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<AttributeDefinition> Attributes { get; set; } = new List<AttributeDefinition>();
}
