using System.Text.Json;
using FormaGeo.Domain.Layers;

namespace FormaGeo.Application.Layers;

public sealed record DataSourceResponse(
    Guid Id,
    string Name,
    string Organization,
    string LicenseName,
    string? LicenseUrl,
    string Attribution,
    string? SourceUrl);

public sealed record LayerVersionResponse(
    Guid Id,
    string VersionLabel,
    DateTimeOffset LastUpdatedAtUtc,
    LayerDeliveryMethod DeliveryMethod,
    string DataUrl,
    string? SourceLayer,
    int? MinimumZoom,
    int? MaximumZoom);

public sealed record LayerResponse(
    Guid Id,
    string Name,
    string Description,
    LayerCategory Category,
    string GeographicCoverage,
    string CoordinateSystem,
    LayerGeometryType GeometryType,
    string FeatureNameProperty,
    JsonElement Style,
    DataSourceResponse DataSource,
    LayerVersionResponse Version);

public sealed record LayerLegendItemResponse(
    Guid Id,
    string Label,
    string FillColor,
    string StrokeColor,
    string? Symbol,
    int SortOrder);

public sealed record LayerLegendResponse(
    Guid LayerId,
    string LayerName,
    IReadOnlyList<LayerLegendItemResponse> Items);

public sealed record ProjectLayerResponse(
    Guid LayerId,
    bool IsVisible,
    decimal Opacity,
    int SortOrder,
    string? Filter,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProjectLayerPreferenceRequest(
    Guid LayerId,
    bool IsVisible,
    decimal Opacity,
    int SortOrder,
    string? Filter);

public sealed record UpdateProjectLayersRequest(
    IReadOnlyList<ProjectLayerPreferenceRequest> Layers);
