using CvManagement.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class PositionAttributeConfiguration : IEntityTypeConfiguration<PositionAttribute>
{
    public void Configure(EntityTypeBuilder<PositionAttribute> builder)
    {
        builder.HasIndex(pa => new { pa.PositionId, pa.AttributeId }).IsUnique();
        builder.Property(pa => pa.RowVersion).IsRowVersion();
    }
}
