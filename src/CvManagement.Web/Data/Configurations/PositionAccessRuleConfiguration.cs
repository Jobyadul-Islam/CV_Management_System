using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class PositionAccessRuleConfiguration : IEntityTypeConfiguration<PositionAccessRule>
{
    public void Configure(EntityTypeBuilder<PositionAccessRule> builder)
    {
        builder.Property(r => r.ComparisonValueString).HasMaxLength(400);
        builder.Property(r => r.ComparisonValueNumeric).HasColumnType("decimal(18,4)");
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne(r => r.ComparisonOption)
            .WithMany()
            .HasForeignKey(r => r.ComparisonOptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
