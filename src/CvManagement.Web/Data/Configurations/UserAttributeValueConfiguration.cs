using CvManagement.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class UserAttributeValueConfiguration : IEntityTypeConfiguration<UserAttributeValue>
{
    public void Configure(EntityTypeBuilder<UserAttributeValue> builder)
    {
        builder.HasIndex(v => new { v.UserId, v.AttributeId }).IsUnique();

        builder.Property(v => v.ValueString).HasMaxLength(400);
        builder.Property(v => v.ValueImageUrl).HasMaxLength(500);
        builder.Property(v => v.ValueNumeric).HasColumnType("decimal(18,4)");
        builder.Property(v => v.RowVersion).IsRowVersion();

        builder.HasOne(v => v.User)
            .WithMany(u => u.AttributeValues)
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // If the selected dropdown option is deleted, the value degrades to "unset" (red-highlighted)
        // rather than blocking the option's deletion.
        builder.HasOne(v => v.ValueOption)
            .WithMany()
            .HasForeignKey(v => v.ValueOptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
