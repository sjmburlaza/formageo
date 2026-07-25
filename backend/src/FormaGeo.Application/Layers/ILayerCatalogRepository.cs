using FormaGeo.Domain.Layers;

namespace FormaGeo.Application.Layers;

public interface ILayerCatalogRepository
{
    Task<IReadOnlyList<LayerDefinition>> GetCatalogAsync(
        CancellationToken cancellationToken = default);

    Task<LayerDefinition?> GetByIdAsync(
        Guid layerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectLayer>> GetProjectLayersAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task ReplaceProjectLayersAsync(
        Guid projectId,
        IReadOnlyList<ProjectLayer> projectLayers,
        CancellationToken cancellationToken = default);

    Task<bool> AllLayersExistAsync(
        IReadOnlyCollection<Guid> layerIds,
        CancellationToken cancellationToken = default);
}
