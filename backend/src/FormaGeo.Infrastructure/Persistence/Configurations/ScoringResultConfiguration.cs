using FormaGeo.Domain.Scoring;
using FormaGeo.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class ScoringResultConfiguration
    : IEntityTypeConfiguration<ScoringResult>
{
    public void Configure(
        EntityTypeBuilder<ScoringResult> builder)
    {
        builder.ToTable("scoring_results");

        builder.HasKey(result => result.Id);

        builder.Property(result => result.Id)
            .HasColumnName("id");

        builder.Property(result => result.SiteId)
            .HasColumnName("site_id")
            .IsRequired();

        builder.Property(result => result.ScoringScenarioId)
            .HasColumnName("scoring_scenario_id")
            .IsRequired();

        builder.Property(result => result.ScoringModelId)
            .HasColumnName("scoring_model_id")
            .IsRequired();

        builder.Property(result => result.OverallScore)
            .HasColumnName("overall_score")
            .HasPrecision(6, 2);

        builder.Property(result => result.Rating)
            .HasColumnName("rating")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(result => result.IsScoreable)
            .HasColumnName("is_scoreable")
            .IsRequired();

        builder.Property(result => result.BreakdownJson)
            .HasColumnName("breakdown")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(result => result.CalculatedAtUtc)
            .HasColumnName("calculated_at_utc")
            .IsRequired();

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(result => result.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ScoringScenario>()
            .WithMany()
            .HasForeignKey(result => result.ScoringScenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ScoringModel>()
            .WithMany()
            .HasForeignKey(result => result.ScoringModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(result => new
        {
            result.ScoringScenarioId,
            result.SiteId,
            result.CalculatedAtUtc
        });
    }
}
