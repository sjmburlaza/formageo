using FormaGeo.Domain.Layers;
using FormaGeo.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FormaGeo.Infrastructure.Persistence.Configurations;

internal static class LayerCatalogSeed
{
    internal static readonly Guid DataSourceId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    internal static readonly Guid BoundariesLayerId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    internal static readonly Guid PlanningLayerId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");
    internal static readonly Guid HazardsLayerId =
        Guid.Parse("20000000-0000-0000-0000-000000000003");
    internal static readonly Guid EnvironmentLayerId =
        Guid.Parse("20000000-0000-0000-0000-000000000004");
    internal static readonly Guid TransportLayerId =
        Guid.Parse("20000000-0000-0000-0000-000000000005");
    internal static readonly Guid FacilitiesLayerId =
        Guid.Parse("20000000-0000-0000-0000-000000000006");

    internal static readonly Guid[] LayerIds =
    [
        BoundariesLayerId,
        PlanningLayerId,
        HazardsLayerId,
        EnvironmentLayerId,
        TransportLayerId,
        FacilitiesLayerId
    ];
}

public sealed class DataSourceConfiguration
    : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("data_sources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id)
            .HasColumnName("id");
        builder.Property(source => source.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(source => source.Organization)
            .HasColumnName("organization")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(source => source.LicenseName)
            .HasColumnName("license_name")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(source => source.LicenseUrl)
            .HasColumnName("license_url")
            .HasMaxLength(500);
        builder.Property(source => source.Attribution)
            .HasColumnName("attribution")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(source => source.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(500);

        builder.HasData(new
        {
            Id = LayerCatalogSeed.DataSourceId,
            Name = "FormaGeo contextual demonstration data",
            Organization = "FormaGeo",
            LicenseName = "CC0 1.0",
            LicenseUrl =
                "https://creativecommons.org/publicdomain/zero/1.0/",
            Attribution =
                "FormaGeo demonstration data — illustrative only",
            SourceUrl = (string?)null
        });
    }
}

public sealed class LayerDefinitionConfiguration
    : IEntityTypeConfiguration<LayerDefinition>
{
    public void Configure(
        EntityTypeBuilder<LayerDefinition> builder)
    {
        builder.ToTable("layer_definitions");
        builder.HasKey(layer => layer.Id);
        builder.Property(layer => layer.Id)
            .HasColumnName("id");
        builder.Property(layer => layer.DataSourceId)
            .HasColumnName("data_source_id")
            .IsRequired();
        builder.Property(layer => layer.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(layer => layer.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(layer => layer.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(layer => layer.GeographicCoverage)
            .HasColumnName("geographic_coverage")
            .HasMaxLength(300)
            .IsRequired();
        builder.Property(layer => layer.CoordinateSystem)
            .HasColumnName("coordinate_system")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(layer => layer.GeometryType)
            .HasColumnName("geometry_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(layer => layer.FeatureNameProperty)
            .HasColumnName("feature_name_property")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(layer => layer.StyleJson)
            .HasColumnName("style_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(layer => layer.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasOne(layer => layer.DataSource)
            .WithMany()
            .HasForeignKey(layer => layer.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(layer => layer.Category);
        builder.HasIndex(layer => layer.IsActive);

        builder.HasData(
            SeedLayer(
                LayerCatalogSeed.BoundariesLayerId,
                "Planning districts",
                "Illustrative administrative boundaries for testing overlay workflows.",
                LayerCategory.Boundaries,
                LayerGeometryType.Polygon,
                """{"fillColor":"#6366f1","strokeColor":"#3730a3","strokeWidth":2}"""),
            SeedLayer(
                LayerCatalogSeed.PlanningLayerId,
                "Land-use zones",
                "Illustrative generalized land-use areas for contextual analysis.",
                LayerCategory.Planning,
                LayerGeometryType.Polygon,
                """{"fillColor":"#f59e0b","strokeColor":"#b45309","strokeWidth":1.5}"""),
            SeedLayer(
                LayerCatalogSeed.HazardsLayerId,
                "Flood susceptibility",
                "Illustrative flood susceptibility areas; not suitable for risk decisions.",
                LayerCategory.Hazards,
                LayerGeometryType.Polygon,
                """{"fillColor":"#0ea5e9","strokeColor":"#0369a1","strokeWidth":1.5}"""),
            SeedLayer(
                LayerCatalogSeed.EnvironmentLayerId,
                "Green and protected areas",
                "Illustrative environmental areas for testing planning overlays.",
                LayerCategory.Environment,
                LayerGeometryType.Polygon,
                """{"fillColor":"#22c55e","strokeColor":"#15803d","strokeWidth":1.5}"""),
            SeedLayer(
                LayerCatalogSeed.TransportLayerId,
                "Primary transport corridors",
                "Illustrative transport links for testing line overlays.",
                LayerCategory.Transport,
                LayerGeometryType.LineString,
                """{"lineColor":"#ef4444","lineWidth":3}"""),
            SeedLayer(
                LayerCatalogSeed.FacilitiesLayerId,
                "Community facilities",
                "Illustrative facility locations for testing point overlays and identification.",
                LayerCategory.Facilities,
                LayerGeometryType.Point,
                """{"circleColor":"#8b5cf6","circleRadius":7,"strokeColor":"#ffffff","strokeWidth":2}"""));
    }

    private static object SeedLayer(
        Guid id,
        string name,
        string description,
        LayerCategory category,
        LayerGeometryType geometryType,
        string styleJson)
    {
        return new
        {
            Id = id,
            DataSourceId = LayerCatalogSeed.DataSourceId,
            Name = name,
            Description = description,
            Category = category,
            GeographicCoverage = "Metro Manila demonstration extent",
            CoordinateSystem = "EPSG:4326",
            GeometryType = geometryType,
            FeatureNameProperty = "name",
            StyleJson = styleJson,
            IsActive = true
        };
    }
}

public sealed class LayerVersionConfiguration
    : IEntityTypeConfiguration<LayerVersion>
{
    public void Configure(
        EntityTypeBuilder<LayerVersion> builder)
    {
        builder.ToTable("layer_versions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id)
            .HasColumnName("id");
        builder.Property(version => version.LayerDefinitionId)
            .HasColumnName("layer_definition_id")
            .IsRequired();
        builder.Property(version => version.VersionLabel)
            .HasColumnName("version_label")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(version => version.LastUpdatedAtUtc)
            .HasColumnName("last_updated_at_utc")
            .IsRequired();
        builder.Property(version => version.DeliveryMethod)
            .HasColumnName("delivery_method")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(version => version.DataUrl)
            .HasColumnName("data_url")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(version => version.SourceLayer)
            .HasColumnName("source_layer")
            .HasMaxLength(200);
        builder.Property(version => version.MinimumZoom)
            .HasColumnName("minimum_zoom");
        builder.Property(version => version.MaximumZoom)
            .HasColumnName("maximum_zoom");
        builder.Property(version => version.IsCurrent)
            .HasColumnName("is_current")
            .IsRequired();

        builder.HasOne<LayerDefinition>()
            .WithMany(layer => layer.Versions)
            .HasForeignKey(version => version.LayerDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(version => new
        {
            version.LayerDefinitionId,
            version.IsCurrent
        });

        var lastUpdated =
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            SeedVersion(1, "planning-districts.geojson", lastUpdated),
            SeedVersion(2, "land-use-zones.geojson", lastUpdated),
            SeedVersion(3, "flood-susceptibility.geojson", lastUpdated),
            SeedVersion(4, "green-areas.geojson", lastUpdated),
            SeedVersion(5, "transport-corridors.geojson", lastUpdated),
            SeedVersion(6, "community-facilities.geojson", lastUpdated));
    }

    private static object SeedVersion(
        int index,
        string filename,
        DateTimeOffset lastUpdated)
    {
        return new
        {
            Id = Guid.Parse(
                $"30000000-0000-0000-0000-{index:000000000000}"),
            LayerDefinitionId =
                LayerCatalogSeed.LayerIds[index - 1],
            VersionLabel = "2026.07-demo",
            LastUpdatedAtUtc = lastUpdated,
            DeliveryMethod = LayerDeliveryMethod.GeoJson,
            DataUrl = $"/layers/{filename}",
            SourceLayer = (string?)null,
            MinimumZoom = (int?)null,
            MaximumZoom = (int?)null,
            IsCurrent = true
        };
    }
}

public sealed class LayerLegendConfiguration
    : IEntityTypeConfiguration<LayerLegend>
{
    public void Configure(
        EntityTypeBuilder<LayerLegend> builder)
    {
        builder.ToTable("layer_legends");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasColumnName("id");
        builder.Property(item => item.LayerDefinitionId)
            .HasColumnName("layer_definition_id")
            .IsRequired();
        builder.Property(item => item.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.FillColor)
            .HasColumnName("fill_color")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(item => item.StrokeColor)
            .HasColumnName("stroke_color")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(item => item.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(80);
        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasOne<LayerDefinition>()
            .WithMany(layer => layer.LegendItems)
            .HasForeignKey(item => item.LayerDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new
        {
            item.LayerDefinitionId,
            item.SortOrder
        });

        builder.HasData(
            SeedLegend(1, "District boundary", "#6366f1", "#3730a3"),
            SeedLegend(2, "Mixed-use zone", "#f59e0b", "#b45309"),
            SeedLegend(3, "Moderate susceptibility", "#0ea5e9", "#0369a1"),
            SeedLegend(4, "Green / protected area", "#22c55e", "#15803d"),
            SeedLegend(5, "Primary corridor", "#ef4444", "#991b1b"),
            SeedLegend(6, "Community facility", "#8b5cf6", "#ffffff"));
    }

    private static object SeedLegend(
        int index,
        string label,
        string fillColor,
        string strokeColor)
    {
        return new
        {
            Id = Guid.Parse(
                $"40000000-0000-0000-0000-{index:000000000000}"),
            LayerDefinitionId =
                LayerCatalogSeed.LayerIds[index - 1],
            Label = label,
            FillColor = fillColor,
            StrokeColor = strokeColor,
            Symbol = (string?)null,
            SortOrder = 0
        };
    }
}

public sealed class ProjectLayerConfiguration
    : IEntityTypeConfiguration<ProjectLayer>
{
    public void Configure(
        EntityTypeBuilder<ProjectLayer> builder)
    {
        builder.ToTable("project_layers");
        builder.HasKey(layer => new
        {
            layer.ProjectId,
            layer.LayerDefinitionId
        });
        builder.Property(layer => layer.ProjectId)
            .HasColumnName("project_id");
        builder.Property(layer => layer.LayerDefinitionId)
            .HasColumnName("layer_definition_id");
        builder.Property(layer => layer.IsVisible)
            .HasColumnName("is_visible")
            .IsRequired();
        builder.Property(layer => layer.Opacity)
            .HasColumnName("opacity")
            .HasPrecision(4, 3)
            .IsRequired();
        builder.Property(layer => layer.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();
        builder.Property(layer => layer.Filter)
            .HasColumnName("filter")
            .HasMaxLength(200);
        builder.Property(layer => layer.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(layer => layer.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LayerDefinition>()
            .WithMany()
            .HasForeignKey(layer => layer.LayerDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(layer => new
        {
            layer.ProjectId,
            layer.SortOrder
        });
    }
}
