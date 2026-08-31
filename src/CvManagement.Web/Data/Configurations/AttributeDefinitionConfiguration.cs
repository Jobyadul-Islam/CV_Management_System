using CvManagement.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Web.Data.Configurations;

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(1000);
        builder.HasIndex(a => a.Name).IsUnique();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.Property(a => a.RegexPattern).HasMaxLength(500);
        builder.Property(a => a.MinValue).HasPrecision(18, 4);
        builder.Property(a => a.MaxValue).HasPrecision(18, 4);

        builder.HasOne(a => a.Category)
            .WithMany(c => c.Attributes)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Options)
            .WithOne(o => o.Attribute)
            .HasForeignKey(o => o.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: UserAttributeValue is also reachable from Attributes indirectly via
        // AttributeOptions -> SetNull, and SQL Server rejects the resulting multi-path cascade.
        // AttributeService.DeleteAsync deletes a definition's values explicitly, in a transaction.
        builder.HasMany(a => a.Values)
            .WithOne(v => v.Attribute)
            .HasForeignKey(v => v.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);

        // A Recruiter must detach the attribute from every position template/rule before it
        // can be deleted from the library -- deletion is a distinct, deliberate action.
        builder.HasMany(a => a.PositionAttributes)
            .WithOne(pa => pa.Attribute)
            .HasForeignKey(pa => pa.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.AccessRules)
            .WithOne(r => r.Attribute)
            .HasForeignKey(r => r.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
