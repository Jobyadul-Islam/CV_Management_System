using CvManagement.Web.Domain;
using CvManagement.Web.ViewModels.Attribute;
using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Web.Services.Implementations;

/// <summary>
/// Builds the shared AttributeValueViewModel from an AttributeDefinition and a possibly-absent
/// UserAttributeValue. Used by the Profile Me/Info tabs (where a row always exists) and CV rendering
/// (where a template attribute may have no value row yet -- "not presented in profile" -> empty).
/// </summary>
public static class AttributeValueMapper
{
    public static AttributeValueViewModel ToViewModel(AttributeDefinition attribute, UserAttributeValue? value)
    {
        var model = new AttributeValueViewModel
        {
            AttributeId = attribute.Id,
            AttributeName = attribute.Name,
            Description = attribute.Description,
            DataType = attribute.DataType,
            Options = attribute.Options.Select(o => new AttributePickerOptionViewModel { Id = o.Id, Label = o.Label }).ToList()
        };

        if (value is null)
        {
            model.IsEmpty = true;
            model.RowVersion = null; // no row yet -- first edit inserts one
            return model;
        }

        model.RowVersion = Convert.ToBase64String(value.RowVersion);
        model.IsEmpty = AttributeEmptiness.IsEmpty(value, attribute.DataType);
        model.ValueString = value.ValueString;
        model.ValueText = value.ValueText;
        model.ValueImageUrl = value.ValueImageUrl;
        model.ValueNumeric = value.ValueNumeric;
        model.ValueDate = value.ValueDate;
        model.ValuePeriodStart = value.ValuePeriodStart;
        model.ValuePeriodEnd = value.ValuePeriodEnd;
        model.ValueBoolean = value.ValueBoolean;
        model.ValueOptionId = value.ValueOptionId;
        return model;
    }
}
