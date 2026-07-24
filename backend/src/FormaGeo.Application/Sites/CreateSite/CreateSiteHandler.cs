using FormaGeo.Application.Projects;
using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.Mapping;
using FormaGeo.Domain.Sites;

namespace FormaGeo.Application.Sites.CreateSite;

public sealed class CreateSiteHandler
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;

    public CreateSiteHandler(
        IProjectRepository projectRepository,
        ISiteRepository siteRepository)
    {
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
    }

    public async Task<SiteResponse> HandleAsync(
        Guid projectId,
        CreateSiteRequest request,
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

        var polygon =
            GeoJsonPolygonMapper.ToDomain(
                request.Boundary);

        var site = Site.Create(
            projectId,
            request.Name,
            polygon);

        await _siteRepository.AddAsync(
            site,
            cancellationToken);

        return SiteResponse.FromDomain(site);
    }
}