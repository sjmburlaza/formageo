using FormaGeo.Domain.Comparisons;
using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class SiteComparisonConfiguration
    : IEntityTypeConfiguration<SiteComparison>
{
    public void Configure(
        EntityTypeBuilder<SiteComparison> builder)
    {
        builder.ToTable("site_comparisons");

        builder.HasKey(comparison => comparison.Id);

        builder.Property(comparison => comparison.Id)
            .HasColumnName("id");

        builder.Property(comparison => comparison.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(comparison => comparison.ScoringScenarioId)
            .HasColumnName("scoring_scenario_id")
            .IsRequired();

        builder.Property(comparison => comparison.ScoringModelId)
            .HasColumnName("scoring_model_id")
            .IsRequired();

        builder.Property(comparison => comparison.SnapshotJson)
            .HasColumnName("snapshot")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(comparison => comparison.ComparisonDateUtc)
            .HasColumnName("comparison_date_utc")
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(comparison => comparison.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ScoringScenario>()
            .WithMany()
            .HasForeignKey(comparison => comparison.ScoringScenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ScoringModel>()
            .WithMany()
            .HasForeignKey(comparison => comparison.ScoringModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(comparison => new
        {
            comparison.ProjectId,
            comparison.ComparisonDateUtc
        });
    }
}
