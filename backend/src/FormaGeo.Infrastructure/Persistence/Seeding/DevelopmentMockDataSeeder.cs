using FormaGeo.Domain.Layers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FormaGeo.Infrastructure.Persistence.Seeding;

public sealed class DevelopmentMockDataSeeder
{
    private readonly FormaGeoDbContext _dbContext;
    private readonly ILogger<DevelopmentMockDataSeeder> _logger;

    public DevelopmentMockDataSeeder(
        FormaGeoDbContext dbContext,
        ILogger<DevelopmentMockDataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Name ==
                    DevelopmentMockData.ProjectName,
                cancellationToken);

        var createdProject = false;

        if (project is null)
        {
            project = DevelopmentMockData.CreateProject();
            _dbContext.Projects.Add(project);
            await _dbContext.SaveChangesAsync(
                cancellationToken);
            createdProject = true;
        }

        var existingSiteNames = await _dbContext.Sites
            .Where(site => site.ProjectId == project.Id)
            .Select(site => site.Name)
            .ToHashSetAsync(
                StringComparer.Ordinal,
                cancellationToken);
        var missingSites = DevelopmentMockData
            .CreateSites(project.Id)
            .Where(site =>
                !existingSiteNames.Contains(site.Name))
            .ToArray();

        if (missingSites.Length > 0)
        {
            _dbContext.Sites.AddRange(missingSites);
        }

        var existingLayerIds =
            await _dbContext.ProjectLayers
                .Where(layer =>
                    layer.ProjectId == project.Id)
                .Select(layer =>
                    layer.LayerDefinitionId)
                .ToHashSetAsync(cancellationToken);
        var layerDefinitions =
            await _dbContext.LayerDefinitions
                .AsNoTracking()
                .Where(layer => layer.IsActive)
                .OrderBy(layer => layer.Category)
                .ThenBy(layer => layer.Name)
                .ToListAsync(cancellationToken);
        var missingProjectLayers = layerDefinitions
            .Where(layer =>
                !existingLayerIds.Contains(layer.Id))
            .Select((layer, index) =>
                ProjectLayer.Create(
                    project.Id,
                    layer.Id,
                    isVisible: true,
                    OpacityFor(layer.Category),
                    existingLayerIds.Count + index))
            .ToArray();

        if (missingProjectLayers.Length > 0)
        {
            _dbContext.ProjectLayers.AddRange(
                missingProjectLayers);
        }

        if (missingSites.Length > 0 ||
            missingProjectLayers.Length > 0)
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        _logger.LogInformation(
            "Development mock data ready: project {ProjectId}, " +
            "{SiteCount} sites added, {LayerCount} layer preferences added, " +
            "project created: {ProjectCreated}.",
            project.Id,
            missingSites.Length,
            missingProjectLayers.Length,
            createdProject);
    }

    private static decimal OpacityFor(
        LayerCategory category)
    {
        return category switch
        {
            LayerCategory.Boundaries => 0.35m,
            LayerCategory.Planning => 0.45m,
            LayerCategory.Hazards => 0.50m,
            LayerCategory.Environment => 0.45m,
            LayerCategory.Transport => 0.85m,
            LayerCategory.Facilities => 1.00m,
            _ => 0.60m
        };
    }
}
