using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class PositionProjectTagConfiguration : IEntityTypeConfiguration<PositionProjectTag>
{
    public void Configure(EntityTypeBuilder<PositionProjectTag> builder)
    {
        builder.HasKey(pt => new { pt.PositionId, pt.TagId });

        builder.HasOne(pt => pt.Tag)
            .WithMany(t => t.PositionTags)
            .HasForeignKey(pt => pt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
