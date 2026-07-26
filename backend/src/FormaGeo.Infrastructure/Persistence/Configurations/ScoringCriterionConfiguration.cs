using FormaGeo.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class ScoringCriterionConfiguration
    : IEntityTypeConfiguration<ScoringCriterion>
{
    public void Configure(
        EntityTypeBuilder<ScoringCriterion> builder)
    {
        builder.ToTable("scoring_criteria");

        builder.HasKey(criterion => criterion.Id);

        builder.Property(criterion => criterion.Id)
            .HasColumnName("id");

        builder.Property(criterion => criterion.ScoringModelId)
            .HasColumnName("scoring_model_id")
            .IsRequired();

        builder.Property(criterion => criterion.Key)
            .HasColumnName("criterion_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(criterion => criterion.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(criterion => criterion.Weight)
            .HasColumnName("weight")
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(criterion => criterion.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(criterion =>
                criterion.NormalizationMethod)
            .HasColumnName("normalization_method")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(criterion => criterion.DataSource)
            .HasColumnName("data_source")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(criterion => criterion.Unit)
            .HasColumnName("unit")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(criterion =>
                criterion.LowerThreshold)
            .HasColumnName("lower_threshold")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(criterion =>
                criterion.UpperThreshold)
            .HasColumnName("upper_threshold")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(criterion =>
                criterion.MissingDataBehavior)
            .HasColumnName("missing_data_behavior")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(criterion => criterion.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(criterion => new
        {
            criterion.ScoringModelId,
            criterion.Key
        }).IsUnique();
    }
}
