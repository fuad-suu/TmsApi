using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;


public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        // builder.Property(s => s.Email)
        //     .IsRequired()
        //     .HasMaxLength(150);

        builder.Property(s => s.GPA)
            .HasPrecision(3, 2);
        
        // Shadow Property for Audit
        builder.Property<DateTime>("LastUpdated");

        // Row Version Concurrency Token (maps to PostgreSQL xmin system column automatically)
        builder.Property(s => s.Version)
            .IsRowVersion();
        // Global Soft-Delete Filter: Automatically excludes soft-deleted students
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}