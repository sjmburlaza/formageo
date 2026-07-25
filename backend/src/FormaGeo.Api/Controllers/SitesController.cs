using FormaGeo.Application.Sites.ArchiveSite;
using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.GetSite;
using FormaGeo.Application.Sites.Mapping;
using FormaGeo.Application.Sites.RestoreSite;
using FormaGeo.Application.Sites.UpdateSite;
using FormaGeo.Application.Sites.UpdateSiteBoundary;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/sites")]
[Tags("Sites")]
public sealed class SitesController : ControllerBase
{
    private readonly DeleteSiteHandler _deleteSiteHandler;
    private readonly GetSiteHandler _getSiteHandler;
    private readonly UpdateSiteHandler _updateSiteHandler;
    private readonly UpdateSiteBoundaryHandler _updateSiteBoundaryHandler;
    private readonly ArchiveSiteHandler _archiveSiteHandler;
    private readonly RestoreSiteHandler _restoreSiteHandler;

    public SitesController(
        DeleteSiteHandler deleteSiteHandler,
        GetSiteHandler getSiteHandler,
        UpdateSiteHandler updateSiteHandler,
        UpdateSiteBoundaryHandler updateSiteBoundaryHandler,
        ArchiveSiteHandler archiveSiteHandler,
        RestoreSiteHandler restoreSiteHandler)
    {
        _deleteSiteHandler = deleteSiteHandler;
        _getSiteHandler = getSiteHandler;
        _updateSiteHandler = updateSiteHandler;
        _updateSiteBoundaryHandler = updateSiteBoundaryHandler;
        _archiveSiteHandler = archiveSiteHandler;
        _restoreSiteHandler = restoreSiteHandler;
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

    [HttpPatch("{siteId:guid}", Name = "UpdateSite")]
    [EndpointSummary("Update a site")]
    [EndpointDescription("Renames a site.")]
    [ProducesResponseType(
        typeof(SiteResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteResponse>> UpdateSiteAsync(
        Guid siteId,
        UpdateSiteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _updateSiteHandler.HandleAsync(
                siteId,
                request,
                cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return SiteNotFound(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ValidationError(
                exception.ParamName ?? "name",
                "validation_error",
                exception.Message);
        }
    }

    [HttpPut("{siteId:guid}/boundary", Name = "UpdateSiteBoundary")]
    [EndpointSummary("Update a site boundary")]
    [EndpointDescription(
        "Replaces a site's boundary with a valid GeoJSON Polygon.")]
    [ProducesResponseType(
        typeof(SiteResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(1_048_576)]
    public async Task<ActionResult<SiteResponse>> UpdateBoundaryAsync(
        Guid siteId,
        UpdateSiteBoundaryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _updateSiteBoundaryHandler.HandleAsync(
                siteId,
                request,
                cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return SiteNotFound(exception.Message);
        }
        catch (PolygonValidationException exception)
        {
            return ValidationError(
                "boundary",
                exception.Problem,
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ValidationError(
                exception.ParamName ?? "boundary",
                "validation_error",
                exception.Message);
        }
    }

    [HttpPost("{siteId:guid}/archive", Name = "ArchiveSite")]
    [EndpointSummary("Archive a site")]
    [ProducesResponseType(
        typeof(SiteResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteResponse>> ArchiveSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _archiveSiteHandler.HandleAsync(
                siteId,
                cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return SiteNotFound(exception.Message);
        }
    }

    [HttpPost("{siteId:guid}/restore", Name = "RestoreSite")]
    [EndpointSummary("Restore a site")]
    [ProducesResponseType(
        typeof(SiteResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteResponse>> RestoreSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _restoreSiteHandler.HandleAsync(
                siteId,
                cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return SiteNotFound(exception.Message);
        }
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

    private NotFoundObjectResult SiteNotFound(string message)
    {
        return NotFound(new
        {
            error = message
        });
    }

    private BadRequestObjectResult ValidationError(
        string field,
        string problem,
        string message)
    {
        return BadRequest(new
        {
            field,
            problem,
            message,
            status = StatusCodes.Status400BadRequest
        });
    }
}
