using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

public sealed class SiteConfiguration
    : IEntityTypeConfiguration<Site>
{
    public void Configure(
        EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");

        builder.HasKey(site => site.Id);

        builder.Property(site => site.Id)
            .HasColumnName("id");

        builder.Property(site => site.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(site => site.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(site => site.Boundary)
            .HasColumnName("boundary")
            .HasColumnType("geometry(Polygon,4326)")
            .IsRequired();

        builder.Property(site => site.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(site => site.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(site => site.ProjectId);

        builder.HasIndex(site => site.Boundary)
            .HasMethod("gist");
    }
}