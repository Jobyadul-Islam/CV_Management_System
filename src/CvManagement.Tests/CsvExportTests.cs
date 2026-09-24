using System.Globalization;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Implementations;
using CvManagement.Web.ViewModels.Profile;

namespace CvManagement.Tests;

public class CsvExportTests
{
    [Fact]
    public void Plain_value_is_written_as_is()
        => Assert.Equal("Mary", CvExportService.CsvField("Mary"));

    [Fact]
    public void Value_with_comma_and_quote_is_quoted_and_escaped()
        => Assert.Equal("\"Smith, \"\"JJ\"\"\"", CvExportService.CsvField("Smith, \"JJ\""));

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")")]
    [InlineData("+cmd")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void Formula_like_value_is_neutralized_with_apostrophe(string value)
        => Assert.StartsWith("'", CvExportService.CsvField(value).TrimStart('"'));

    [Fact]
    public void Negative_number_stays_numeric()
        => Assert.Equal("-3.5", CvExportService.CsvField("-3.5"));

    [Fact]
    public void Numeric_value_is_formatted_invariantly_even_under_russian_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru");
        try
        {
            var field = new AttributeValueViewModel { DataType = AttributeDataType.Numeric, ValueNumeric = 3.5m };
            Assert.Equal("3.5", AttributeValueFormatter.ToPlainText(field, TestLocalization.Localizer));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
