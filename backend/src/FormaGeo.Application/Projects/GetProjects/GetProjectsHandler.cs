namespace FormaGeo.Application.Projects.GetProjects;

public sealed class GetProjectsHandler
{
    private readonly IProjectRepository _projectRepository;

    public GetProjectsHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<IReadOnlyList<ProjectResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var projects = await _projectRepository.GetAllAsync(
            cancellationToken);

        return projects
            .Select(ProjectResponse.FromDomain)
            .ToList();
    }
}