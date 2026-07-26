using FormaGeo.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class ScoringModelConfiguration
    : IEntityTypeConfiguration<ScoringModel>
{
    public void Configure(
        EntityTypeBuilder<ScoringModel> builder)
    {
        builder.ToTable("scoring_models");

        builder.HasKey(model => model.Id);

        builder.Property(model => model.Id)
            .HasColumnName("id");

        builder.Property(model => model.ScoringScenarioId)
            .HasColumnName("scoring_scenario_id")
            .IsRequired();

        builder.Property(model => model.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(model => model.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(model => model.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasOne<ScoringScenario>()
            .WithMany()
            .HasForeignKey(model => model.ScoringScenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(model => model.Criteria)
            .WithOne()
            .HasForeignKey(criterion => criterion.ScoringModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(model => new
        {
            model.ScoringScenarioId,
            model.Version
        }).IsUnique();

        builder.Navigation(model => model.Criteria)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}
