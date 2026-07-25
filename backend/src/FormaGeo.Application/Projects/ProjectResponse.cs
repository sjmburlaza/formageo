using FormaGeo.Domain.Projects;

namespace FormaGeo.Application.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    int SiteCount)
{
    public static ProjectResponse FromDomain(
        Project project,
        int siteCount = 0)
    {
        return new ProjectResponse(
            project.Id,
            project.Name,
            project.CreatedAtUtc,
            siteCount);
    }
}
