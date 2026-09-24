using CvManagement.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class CvLikeConfiguration : IEntityTypeConfiguration<CvLike>
{
    public void Configure(EntityTypeBuilder<CvLike> builder)
    {
        builder.HasIndex(l => new { l.CvId, l.RecruiterUserId }).IsUnique();

        // Restrict (not Cascade) to avoid SQL Server's "multiple cascade paths" error: a user delete
        // already cascades to their own Cvs -> those Cvs' Likes; likes THEY gave on others' Cvs are a
        // second path into the same table. AdminUserService.DeleteUserAsync deletes these explicitly.
        builder.HasOne(l => l.RecruiterUser)
            .WithMany()
            .HasForeignKey(l => l.RecruiterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
