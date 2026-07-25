using FormaGeo.Application.Layers;
using FormaGeo.Domain.Layers;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class LayerCatalogRepository
    : ILayerCatalogRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public LayerCatalogRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LayerDefinition>>
        GetCatalogAsync(
            CancellationToken cancellationToken = default)
    {
        return await CatalogQuery()
            .Where(layer => layer.IsActive)
            .OrderBy(layer => layer.Category)
            .ThenBy(layer => layer.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<LayerDefinition?> GetByIdAsync(
        Guid layerId,
        CancellationToken cancellationToken = default)
    {
        return CatalogQuery()
            .SingleOrDefaultAsync(
                layer =>
                    layer.Id == layerId &&
                    layer.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectLayer>>
        GetProjectLayersAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectLayers
            .AsNoTracking()
            .Where(layer => layer.ProjectId == projectId)
            .OrderBy(layer => layer.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceProjectLayersAsync(
        Guid projectId,
        IReadOnlyList<ProjectLayer> projectLayers,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await _dbContext.ProjectLayers
            .Where(layer => layer.ProjectId == projectId)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.ProjectLayers.AddRange(projectLayers);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> AllLayersExistAsync(
        IReadOnlyCollection<Guid> layerIds,
        CancellationToken cancellationToken = default)
    {
        if (layerIds.Count == 0)
        {
            return true;
        }

        var uniqueLayerIds = layerIds.Distinct().ToArray();
        var count = await _dbContext.LayerDefinitions
            .Where(layer =>
                layer.IsActive &&
                uniqueLayerIds.Contains(layer.Id))
            .CountAsync(cancellationToken);

        return count == uniqueLayerIds.Length;
    }

    private IQueryable<LayerDefinition> CatalogQuery()
    {
        return _dbContext.LayerDefinitions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(layer => layer.DataSource)
            .Include(layer => layer.Versions)
            .Include(layer => layer.LegendItems);
    }
}
