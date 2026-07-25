using FormaGeo.Application.Sites;

namespace FormaGeo.Application.Projects.GetProjects;

public sealed class GetProjectsHandler
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;

    public GetProjectsHandler(
        IProjectRepository projectRepository,
        ISiteRepository siteRepository)
    {
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
    }

    public async Task<IReadOnlyList<ProjectResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var projects = await _projectRepository.GetAllAsync(
            cancellationToken);

        var siteCounts =
            await _siteRepository.GetCountsByProjectIdAsync(
                projects.Select(project => project.Id),
                cancellationToken);

        return projects
            .Select(project => ProjectResponse.FromDomain(
                project,
                siteCounts.GetValueOrDefault(project.Id)))
            .ToList();
    }
}
