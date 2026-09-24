using CvManagement.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class CvConfiguration : IEntityTypeConfiguration<Cv>
{
    public void Configure(EntityTypeBuilder<Cv> builder)
    {
        builder.HasIndex(c => new { c.UserId, c.PositionId }).IsUnique();
        builder.HasIndex(c => c.PositionId);
        builder.HasIndex(c => c.Status);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasOne(c => c.User)
            .WithMany(u => u.Cvs)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Likes)
            .WithOne(l => l.Cv)
            .HasForeignKey(l => l.CvId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
