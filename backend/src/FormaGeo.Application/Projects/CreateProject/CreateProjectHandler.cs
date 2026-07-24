using FormaGeo.Domain.Projects;

namespace FormaGeo.Application.Projects.CreateProject;

public sealed class CreateProjectHandler
{
    private readonly IProjectRepository _projectRepository;

    public CreateProjectHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<ProjectResponse> HandleAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = Project.Create(request.Name);

        await _projectRepository.AddAsync(
            project,
            cancellationToken);

        return ProjectResponse.FromDomain(project);
    }
}