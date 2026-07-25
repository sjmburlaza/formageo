using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.GetSite;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/sites")]
[Tags("Sites")]
public sealed class SitesController : ControllerBase
{
    private readonly DeleteSiteHandler _deleteSiteHandler;
    private readonly GetSiteHandler _getSiteHandler;

    public SitesController(
        DeleteSiteHandler deleteSiteHandler,
        GetSiteHandler getSiteHandler)
    {
        _deleteSiteHandler = deleteSiteHandler;
        _getSiteHandler = getSiteHandler;
    }

    [HttpGet("{siteId:guid}", Name = "GetSite")]
    [EndpointSummary("Get a site")]
    [EndpointDescription(
        "Returns one site and its GeoJSON boundary.")]
    [ProducesResponseType(
        typeof(SiteResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteResponse>> GetSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var site = await _getSiteHandler.HandleAsync(
            siteId,
            cancellationToken);

        if (site is null)
        {
            return NotFound(new
            {
                error = $"Site '{siteId}' was not found."
            });
        }

        return Ok(site);
    }

    [HttpDelete("{siteId:guid}", Name = "DeleteSite")]
    [EndpointSummary("Delete a site")]
    [EndpointDescription("Permanently deletes a site.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteSiteHandler.HandleAsync(
            siteId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound(new
            {
                error = $"Site '{siteId}' was not found."
            });
        }

        return NoContent();
    }
}
