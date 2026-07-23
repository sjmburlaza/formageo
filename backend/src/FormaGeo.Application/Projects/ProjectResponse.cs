using FormaGeo.Domain.Projects;

namespace FormaGeo.Application.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc)
{
    public static ProjectResponse FromDomain(Project project)
    {
        return new ProjectResponse(
            project.Id,
            project.Name,
            project.CreatedAtUtc);
    }
}