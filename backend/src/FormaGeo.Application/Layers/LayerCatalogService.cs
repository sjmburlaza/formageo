using System.Text.Json;
using FormaGeo.Application.Projects;
using FormaGeo.Domain.Layers;

namespace FormaGeo.Application.Layers;

public sealed class LayerCatalogService
{
    private readonly ILayerCatalogRepository _layerRepository;
    private readonly IProjectRepository _projectRepository;

    public LayerCatalogService(
        ILayerCatalogRepository layerRepository,
        IProjectRepository projectRepository)
    {
        _layerRepository = layerRepository;
        _projectRepository = projectRepository;
    }

    public async Task<IReadOnlyList<LayerResponse>> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var layers = await _layerRepository.GetCatalogAsync(
            cancellationToken);

        return layers.Select(MapLayer).ToList();
    }

    public async Task<LayerResponse?> GetLayerAsync(
        Guid layerId,
        CancellationToken cancellationToken = default)
    {
        var layer = await _layerRepository.GetByIdAsync(
            layerId,
            cancellationToken);

        return layer is null ? null : MapLayer(layer);
    }

    public async Task<LayerLegendResponse?> GetLegendAsync(
        Guid layerId,
        CancellationToken cancellationToken = default)
    {
        var layer = await _layerRepository.GetByIdAsync(
            layerId,
            cancellationToken);

        if (layer is null)
        {
            return null;
        }

        return new LayerLegendResponse(
            layer.Id,
            layer.Name,
            layer.LegendItems
                .OrderBy(item => item.SortOrder)
                .Select(item => new LayerLegendItemResponse(
                    item.Id,
                    item.Label,
                    item.FillColor,
                    item.StrokeColor,
                    item.Symbol,
                    item.SortOrder))
                .ToList());
    }

    public async Task<IReadOnlyList<ProjectLayerResponse>>
        GetProjectLayersAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        await EnsureProjectExistsAsync(
            projectId,
            cancellationToken);
        var projectLayers =
            await _layerRepository.GetProjectLayersAsync(
                projectId,
                cancellationToken);

        return projectLayers
            .OrderBy(layer => layer.SortOrder)
            .Select(MapProjectLayer)
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectLayerResponse>>
        UpdateProjectLayersAsync(
            Guid projectId,
            UpdateProjectLayersRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Layers);

        await EnsureProjectExistsAsync(
            projectId,
            cancellationToken);

        var duplicateLayerId = request.Layers
            .GroupBy(layer => layer.LayerId)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateLayerId is not null)
        {
            throw new ArgumentException(
                $"Layer '{duplicateLayerId}' is listed more than once.",
                nameof(request));
        }

        var layerIds = request.Layers
            .Select(layer => layer.LayerId)
            .ToArray();

        if (!await _layerRepository.AllLayersExistAsync(
                layerIds,
                cancellationToken))
        {
            throw new KeyNotFoundException(
                "One or more requested layers were not found.");
        }

        var projectLayers = request.Layers
            .Select(layer => ProjectLayer.Create(
                projectId,
                layer.LayerId,
                layer.IsVisible,
                layer.Opacity,
                layer.SortOrder,
                layer.Filter))
            .ToList();

        await _layerRepository.ReplaceProjectLayersAsync(
            projectId,
            projectLayers,
            cancellationToken);

        return projectLayers
            .OrderBy(layer => layer.SortOrder)
            .Select(MapProjectLayer)
            .ToList();
    }

    private async Task EnsureProjectExistsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await _projectRepository.ExistsAsync(
                projectId,
                cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }
    }

    private static LayerResponse MapLayer(
        LayerDefinition layer)
    {
        var version = layer.Versions
            .Where(candidate => candidate.IsCurrent)
            .OrderByDescending(candidate =>
                candidate.LastUpdatedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Layer '{layer.Id}' has no current version.");

        using var styleDocument =
            JsonDocument.Parse(layer.StyleJson);

        return new LayerResponse(
            layer.Id,
            layer.Name,
            layer.Description,
            layer.Category,
            layer.GeographicCoverage,
            layer.CoordinateSystem,
            layer.GeometryType,
            layer.FeatureNameProperty,
            styleDocument.RootElement.Clone(),
            new DataSourceResponse(
                layer.DataSource.Id,
                layer.DataSource.Name,
                layer.DataSource.Organization,
                layer.DataSource.LicenseName,
                layer.DataSource.LicenseUrl,
                layer.DataSource.Attribution,
                layer.DataSource.SourceUrl),
            new LayerVersionResponse(
                version.Id,
                version.VersionLabel,
                version.LastUpdatedAtUtc,
                version.DeliveryMethod,
                version.DataUrl,
                version.SourceLayer,
                version.MinimumZoom,
                version.MaximumZoom));
    }

    private static ProjectLayerResponse MapProjectLayer(
        ProjectLayer projectLayer)
    {
        return new ProjectLayerResponse(
            projectLayer.LayerDefinitionId,
            projectLayer.IsVisible,
            projectLayer.Opacity,
            projectLayer.SortOrder,
            projectLayer.Filter,
            projectLayer.UpdatedAtUtc);
    }
}
