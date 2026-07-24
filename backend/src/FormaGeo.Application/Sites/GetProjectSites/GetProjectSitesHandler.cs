using FormaGeo.Application.Projects;
using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.GetProjectSites;

public sealed class GetProjectSitesHandler
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;

    public GetProjectSitesHandler(
        IProjectRepository projectRepository,
        ISiteRepository siteRepository)
    {
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
    }

    public async Task<IReadOnlyList<SiteResponse>> HandleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var projectExists =
            await _projectRepository.ExistsAsync(
                projectId,
                cancellationToken);

        if (!projectExists)
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }

        var sites =
            await _siteRepository.GetByProjectIdAsync(
                projectId,
                cancellationToken);

        return sites
            .Select(SiteResponse.FromDomain)
            .ToList();
    }
}