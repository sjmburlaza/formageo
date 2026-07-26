using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class ScoringScenarioConfiguration
    : IEntityTypeConfiguration<ScoringScenario>
{
    public void Configure(
        EntityTypeBuilder<ScoringScenario> builder)
    {
        builder.ToTable("scoring_scenarios");

        builder.HasKey(scenario => scenario.Id);

        builder.Property(scenario => scenario.Id)
            .HasColumnName("id");

        builder.Property(scenario => scenario.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(scenario => scenario.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(scenario => scenario.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(scenario => scenario.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(scenario => scenario.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(scenario => scenario.ProjectId);
    }
}
