using CvManagement.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class AttributeCategoryConfiguration : IEntityTypeConfiguration<AttributeCategory>
{
    public void Configure(EntityTypeBuilder<AttributeCategory> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
