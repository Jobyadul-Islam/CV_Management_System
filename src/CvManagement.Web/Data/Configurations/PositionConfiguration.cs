using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ShortDescription).HasMaxLength(1000);
        builder.Property(p => p.Company).HasMaxLength(200);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => p.Title);
        builder.HasIndex(p => p.AccessMode);
        builder.HasIndex(p => p.Level);
        builder.HasIndex(p => p.UpdatedAt);

        builder.HasMany(p => p.PositionAttributes)
            .WithOne(pa => pa.Position)
            .HasForeignKey(pa => pa.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.AccessRules)
            .WithOne(r => r.Position)
            .HasForeignKey(r => r.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProjectTags)
            .WithOne(t => t.Position)
            .HasForeignKey(t => t.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Cvs)
            .WithOne(c => c.Position)
            .HasForeignKey(c => c.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.DiscussionPosts)
            .WithOne(d => d.Position)
            .HasForeignKey(d => d.PositionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
