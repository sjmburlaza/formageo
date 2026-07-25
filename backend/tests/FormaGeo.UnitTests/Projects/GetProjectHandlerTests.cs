using FormaGeo.Application.Projects;
using FormaGeo.Application.Projects.GetProject;
using FormaGeo.Application.Sites;
using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Sites;

namespace FormaGeo.UnitTests.Projects;

public sealed class GetProjectHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsProjectWithSiteCount()
    {
        var project = Project.Create("Central district study");
        var handler = new GetProjectHandler(
            new ProjectRepositoryStub(project),
            new SiteRepositoryStub(
                new Dictionary<Guid, int>
                {
                    [project.Id] = 3
                }));

        var response = await handler.HandleAsync(project.Id);

        Assert.NotNull(response);
        Assert.Equal(project.Id, response.Id);
        Assert.Equal(project.Name, response.Name);
        Assert.Equal(3, response.SiteCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNullWhenProjectDoesNotExist()
    {
        var handler = new GetProjectHandler(
            new ProjectRepositoryStub(),
            new SiteRepositoryStub());

        var response = await handler.HandleAsync(Guid.NewGuid());

        Assert.Null(response);
    }

    private sealed class ProjectRepositoryStub(
        Project? project = null)
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
            IReadOnlyList<Project> projects =
                project is null ? [] : [project];

            return Task.FromResult(projects);
        }

        public Task<Project?> GetByIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                project?.Id == projectId ? project : null);
        }

        public Task<bool> ExistsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                project?.Id == projectId);
        }
    }

    private sealed class SiteRepositoryStub(
        IReadOnlyDictionary<Guid, int>? siteCounts = null)
        : ISiteRepository
    {
        public Task AddAsync(
            Site site,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Site?> GetByIdAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Site?>(null);
        }

        public Task<IReadOnlyList<Site>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Site>>([]);
        }

        public Task<IReadOnlyDictionary<Guid, int>>
            GetCountsByProjectIdAsync(
                IEnumerable<Guid> projectIds,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                siteCounts ??
                new Dictionary<Guid, int>());
        }

        public Task<bool> DeleteAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
