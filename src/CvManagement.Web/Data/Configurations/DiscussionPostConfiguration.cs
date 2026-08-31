using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class DiscussionPostConfiguration : IEntityTypeConfiguration<DiscussionPost>
{
    public void Configure(EntityTypeBuilder<DiscussionPost> builder)
    {
        builder.Property(d => d.AuthorDisplayNameSnapshot).HasMaxLength(200);
        builder.HasIndex(d => new { d.PositionId, d.Id });

        builder.HasOne(d => d.AuthorUser)
            .WithMany()
            .HasForeignKey(d => d.AuthorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
