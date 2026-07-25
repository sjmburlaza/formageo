using FormaGeo.Domain.Analyses;
using FormaGeo.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class AnalysisRunConfiguration
    : IEntityTypeConfiguration<AnalysisRun>
{
    public void Configure(
        EntityTypeBuilder<AnalysisRun> builder)
    {
        builder.ToTable("analysis_runs");

        builder.HasKey(analysisRun => analysisRun.Id);

        builder.Property(analysisRun => analysisRun.Id)
            .HasColumnName("id");

        builder.Property(analysisRun => analysisRun.SiteId)
            .HasColumnName("site_id")
            .IsRequired();

        builder.Property(analysisRun => analysisRun.AnalysisType)
            .HasColumnName("analysis_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(analysisRun => analysisRun.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(analysisRun =>
                analysisRun.InputParametersJson)
            .HasColumnName("input_parameters")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(analysisRun => analysisRun.ResultJson)
            .HasColumnName("result")
            .HasColumnType("jsonb");

        builder.Property(analysisRun => analysisRun.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(analysisRun =>
                analysisRun.RequestedAtUtc)
            .HasColumnName("requested_at_utc")
            .IsRequired();

        builder.Property(analysisRun => analysisRun.StartedAtUtc)
            .HasColumnName("started_at_utc");

        builder.Property(analysisRun => analysisRun.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.Property(analysisRun =>
                analysisRun.AnalysisVersion)
            .HasColumnName("analysis_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(analysisRun => analysisRun.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(analysisRun => analysisRun.SiteId);

        builder.HasIndex(analysisRun => new
        {
            analysisRun.Status,
            analysisRun.RequestedAtUtc
        });
    }
}
