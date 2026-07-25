using FormaGeo.Application.Sites.ArchiveSite;
using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.ExportSite;
using FormaGeo.Application.Sites.GetSite;
using FormaGeo.Application.Sites.GetSiteSummary;
using FormaGeo.Application.Sites.Mapping;
using FormaGeo.Application.Sites.RestoreSite;
using FormaGeo.Application.Sites.UpdateSite;
using FormaGeo.Application.Sites.UpdateSiteBoundary;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/sites")]
[Tags("Sites")]
public sealed class SitesController : ControllerBase
{
    private readonly DeleteSiteHandler _deleteSiteHandler;
    private readonly GetSiteHandler _getSiteHandler;
    private readonly GetSiteSummaryHandler _getSiteSummaryHandler;
    private readonly UpdateSiteHandler _updateSiteHandler;
    private readonly UpdateSiteBoundaryHandler _updateSiteBoundaryHandler;
    private readonly ArchiveSiteHandler _archiveSiteHandler;
    private readonly RestoreSiteHandler _restoreSiteHandler;
    private readonly ExportSiteHandler _exportSiteHandler;

    public SitesController(
        DeleteSiteHandler deleteSiteHandler,
        GetSiteHandler getSiteHandler,
        GetSiteSummaryHandler getSiteSummaryHandler,
        UpdateSiteHandler updateSiteHandler,
        UpdateSiteBoundaryHandler updateSiteBoundaryHandler,
        ArchiveSiteHandler archiveSiteHandler,
        RestoreSiteHandler restoreSiteHandler,
        ExportSiteHandler exportSiteHandler)
    {
        _deleteSiteHandler = deleteSiteHandler;
        _getSiteHandler = getSiteHandler;
        _getSiteSummaryHandler = getSiteSummaryHandler;
        _updateSiteHandler = updateSiteHandler;
        _updateSiteBoundaryHandler = updateSiteBoundaryHandler;
        _archiveSiteHandler = archiveSiteHandler;
        _restoreSiteHandler = restoreSiteHandler;
        _exportSiteHandler = exportSiteHandler;
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

    [HttpGet("{siteId:guid}/summary", Name = "GetSiteSummary")]
    [EndpointSummary("Get a site spatial summary")]
    [EndpointDescription(
        "Returns geodesic area and perimeter measurements, centroid, bounds, and geometry quality information.")]
    [ProducesResponseType(
        typeof(SiteSummaryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteSummaryResponse>>
        GetSiteSummaryAsync(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        var summary =
            await _getSiteSummaryHandler.HandleAsync(
                siteId,
                cancellationToken);

        if (summary is null)
        {
            return NotFound(new
            {
                error = $"Site '{siteId}' was not found."
            });
        }

        return Ok(summary);
    }

    [HttpGet("{siteId:guid}/export", Name = "ExportSite")]
    [EndpointSummary("Export a Site boundary")]
    [EndpointDescription(
        "Downloads a Site as an RFC 7946 GeoJSON Feature in WGS 84.")]
    [Produces("application/geo+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportSiteAsync(
        Guid siteId,
        [FromQuery] string format = "geojson",
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                format,
                "geojson",
                StringComparison.OrdinalIgnoreCase))
        {
            return ValidationError(
                "format",
                "unsupported_export_format",
                "Only the 'geojson' export format is currently supported.");
        }

        var feature = await _exportSiteHandler.HandleAsync(
            siteId,
            cancellationToken);

        if (feature is null)
        {
            return SiteNotFound(
                $"Site '{siteId}' was not found.");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(
            new JsonStringEnumConverter());
        var content = JsonSerializer.SerializeToUtf8Bytes(
            feature,
            options);
        var fileName = CreateExportFileName(
            feature.Properties.Name);

        return File(
            content,
            "application/geo+json",
            fileName);
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

    private static string CreateExportFileName(string siteName)
    {
        var safeCharacters = siteName
            .Trim()
            .ToLowerInvariant()
            .Select(character =>
                char.IsAsciiLetterOrDigit(character)
                    ? character
                    : '-')
            .ToArray();
        var slug = string.Join(
            '-',
            new string(safeCharacters)
                .Split(
                    '-',
                    StringSplitOptions.RemoveEmptyEntries));

        return $"{(string.IsNullOrWhiteSpace(slug) ? "site" : slug)}-boundary.geojson";
    }
}
