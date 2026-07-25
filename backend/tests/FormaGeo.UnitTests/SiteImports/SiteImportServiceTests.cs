using FormaGeo.Application.Projects;
using FormaGeo.Application.SiteImports;
using FormaGeo.Application.Sites;
using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Sites;

namespace FormaGeo.UnitTests.SiteImports;

public sealed class SiteImportServiceTests
{
    [Fact]
    public async Task HandleAsync_ImportsMultiPolygonPiecesSeparately()
    {
        var project = Project.Create("Candidate sites");
        var siteRepository = new SiteRepositoryStub();
        var service = new SiteImportService(
            new ProjectRepositoryStub(project),
            siteRepository);

        var result = await service.HandleAsync(
            project.Id,
            new SiteImportRequest(
                "parcels.geojson",
                """
                {
                  "type": "Feature",
                  "properties": { "name": "Parcel" },
                  "geometry": {
                    "type": "MultiPolygon",
                    "coordinates": [
                      [[
                        [121.0, 14.0],
                        [121.1, 14.0],
                        [121.1, 14.1],
                        [121.0, 14.0]
                      ]],
                      [[
                        [121.2, 14.2],
                        [121.3, 14.2],
                        [121.3, 14.3],
                        [121.2, 14.2]
                      ]]
                    ]
                  }
                }
                """,
                "{feature}",
                SiteImportMode.Separate,
                [0]));

        Assert.Equal(2, result.ImportedSites.Count);
        Assert.Equal(
            ["Parcel 1", "Parcel 2"],
            result.ImportedSites
                .Select(site => site.Name)
                .ToArray());
        Assert.Equal(2, siteRepository.Sites.Count);
    }

    [Fact]
    public async Task HandleAsync_SkipsDuplicateGeometry()
    {
        var project = Project.Create("Candidate sites");
        var siteRepository = new SiteRepositoryStub();
        var service = new SiteImportService(
            new ProjectRepositoryStub(project),
            siteRepository);
        var request = new SiteImportRequest(
            "parcel.geojson",
            PolygonGeoJson,
            "{feature}",
            SiteImportMode.Separate,
            null);

        var first = await service.HandleAsync(
            project.Id,
            request);
        var second = await service.HandleAsync(
            project.Id,
            request);

        Assert.Single(first.ImportedSites);
        Assert.Empty(second.ImportedSites);
        Assert.Contains(
            second.SkippedFeatures,
            skipped => skipped.Reason.Contains(
                "duplicates",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleAsync_RejectsUnsupportedFileExtension()
    {
        var project = Project.Create("Candidate sites");
        var service = new SiteImportService(
            new ProjectRepositoryStub(project),
            new SiteRepositoryStub());

        var exception =
            await Assert.ThrowsAsync<SiteImportException>(
                () => service.HandleAsync(
                    project.Id,
                    new SiteImportRequest(
                        "parcel.kml",
                        PolygonGeoJson,
                        null,
                        SiteImportMode.Separate,
                        null)));

        Assert.Equal(
            "unsupported_file_type",
            exception.Problem);
    }

    private const string PolygonGeoJson =
        """
        {
          "type": "Feature",
          "properties": { "name": "Parcel" },
          "geometry": {
            "type": "Polygon",
            "coordinates": [[
              [121.0, 14.0],
              [121.1, 14.0],
              [121.1, 14.1],
              [121.0, 14.0]
            ]]
          }
        }
        """;

    private sealed class ProjectRepositoryStub(
        Project project)
        : IProjectRepository
    {
        public Task AddAsync(
            Project projectToAdd,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Project>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(
                [project]);
        }

        public Task<Project?> GetByIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                project.Id == projectId ? project : null);
        }

        public Task<bool> ExistsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                project.Id == projectId);
        }
    }

    private sealed class SiteRepositoryStub : ISiteRepository
    {
        public List<Site> Sites { get; } = [];

        public Task AddAsync(
            Site site,
            CancellationToken cancellationToken = default)
        {
            Sites.Add(site);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IEnumerable<Site> sites,
            CancellationToken cancellationToken = default)
        {
            Sites.AddRange(sites);
            return Task.CompletedTask;
        }

        public Task<Site?> GetByIdAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Sites.SingleOrDefault(site =>
                    site.Id == siteId));
        }

        public Task<Site?> GetForUpdateAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return GetByIdAsync(
                siteId,
                cancellationToken);
        }

        public Task<IReadOnlyList<Site>>
            GetByProjectIdAsync(
                Guid projectId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Site>>(
                Sites
                    .Where(site =>
                        site.ProjectId == projectId)
                    .ToArray());
        }

        public Task<IReadOnlyDictionary<Guid, int>>
            GetCountsByProjectIdAsync(
                IEnumerable<Guid> projectIds,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, int> result =
                Sites
                    .GroupBy(site => site.ProjectId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Count());

            return Task.FromResult(result);
        }

        public Task<bool> DeleteAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
