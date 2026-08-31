using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class AttributeOptionConfiguration : IEntityTypeConfiguration<AttributeOption>
{
    public void Configure(EntityTypeBuilder<AttributeOption> builder)
    {
        builder.Property(o => o.Label).HasMaxLength(200).IsRequired();
        builder.HasIndex(o => new { o.AttributeId, o.Label }).IsUnique();
        builder.Property(o => o.RowVersion).IsRowVersion();
    }
}
