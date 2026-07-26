using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class GeneratedReportConfiguration
    : IEntityTypeConfiguration<GeneratedReport>
{
    public void Configure(
        EntityTypeBuilder<GeneratedReport> builder)
    {
        builder.ToTable("generated_reports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasColumnName("id");

        builder.Property(report => report.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(report => report.SourceType)
            .HasColumnName("source_type")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(report => report.SiteId)
            .HasColumnName("site_id");

        builder.Property(report => report.ComparisonId)
            .HasColumnName("comparison_id");

        builder.Property(report => report.Format)
            .HasColumnName("format")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(report => report.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(report => report.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(240)
            .IsRequired();

        builder.Property(report => report.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(report => report.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(report => report.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(report => report.SectionsJson)
            .HasColumnName("sections")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(report => report.BrandingJson)
            .HasColumnName("branding")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(report => report.GeneratedAtUtc)
            .HasColumnName("generated_at_utc")
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(report => report.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(report => report.StorageKey)
            .IsUnique();

        builder.HasIndex(report => new
        {
            report.SiteId,
            report.GeneratedAtUtc
        });

        builder.HasIndex(report => new
        {
            report.ComparisonId,
            report.GeneratedAtUtc
        });

        builder.HasIndex(report => new
        {
            report.ProjectId,
            report.GeneratedAtUtc
        });
    }
}
