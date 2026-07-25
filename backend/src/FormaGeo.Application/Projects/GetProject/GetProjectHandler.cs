using FormaGeo.Application.Sites;

namespace FormaGeo.Application.Projects.GetProject;

public sealed class GetProjectHandler
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;

    public GetProjectHandler(
        IProjectRepository projectRepository,
        ISiteRepository siteRepository)
    {
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
    }

    public async Task<ProjectResponse?> HandleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null)
        {
            return null;
        }

        var siteCounts =
            await _siteRepository.GetCountsByProjectIdAsync(
                [projectId],
                cancellationToken);

        return ProjectResponse.FromDomain(
            project,
            siteCounts.GetValueOrDefault(projectId));
    }
}
