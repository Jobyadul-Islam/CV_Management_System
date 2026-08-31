using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class AttributeUsageConfiguration : IEntityTypeConfiguration<AttributeUsage>
{
    public void Configure(EntityTypeBuilder<AttributeUsage> builder)
    {
        builder.HasIndex(u => new { u.UserId, u.AttributeId }).IsUnique();
        builder.HasIndex(u => new { u.UserId, u.LastUsedAt });

        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.Attribute)
            .WithMany()
            .HasForeignKey(u => u.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
