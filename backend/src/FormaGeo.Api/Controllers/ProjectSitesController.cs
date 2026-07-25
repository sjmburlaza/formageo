using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.CreateSite;
using FormaGeo.Application.Sites.GetProjectSites;
using FormaGeo.Application.Sites.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/sites")]
[Tags("Sites")]
public sealed class ProjectSitesController : ControllerBase
{
    private readonly CreateSiteHandler _createSiteHandler;
    private readonly GetProjectSitesHandler _getProjectSitesHandler;

    public ProjectSitesController(
        CreateSiteHandler createSiteHandler,
        GetProjectSitesHandler getProjectSitesHandler)
    {
        _createSiteHandler = createSiteHandler;
        _getProjectSitesHandler = getProjectSitesHandler;
    }

    [HttpPost(Name = "CreateSite")]
    [EndpointSummary("Create a site")]
    [EndpointDescription(
        "Creates a site with a GeoJSON Polygon boundary.")]
    [Consumes("application/json")]
    [ProducesResponseType(
        typeof(SiteResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(1_048_576)]
    public async Task<ActionResult<SiteResponse>> CreateSiteAsync(
        Guid projectId,
        CreateSiteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await _createSiteHandler.HandleAsync(
                projectId,
                request,
                cancellationToken);

            return Created(
                $"/api/sites/{site.Id}",
                site);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                field = "projectId",
                problem = "not_found",
                message = exception.Message,
                status = StatusCodes.Status404NotFound
            });
        }
        catch (PolygonValidationException exception)
        {
            return BadRequest(new
            {
                field = "boundary",
                problem = exception.Problem,
                message = exception.Message,
                status = StatusCodes.Status400BadRequest
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                field = exception.ParamName ?? "request",
                problem = "validation_error",
                message = exception.Message,
                status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet(Name = "GetProjectSites")]
    [EndpointSummary("List project sites")]
    [EndpointDescription(
        "Returns every site belonging to a project.")]
    [ProducesResponseType(
        typeof(IReadOnlyList<SiteResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SiteResponse>>>
        GetProjectSitesAsync(
            Guid projectId,
            CancellationToken cancellationToken)
    {
        try
        {
            var sites = await _getProjectSitesHandler.HandleAsync(
                projectId,
                cancellationToken);

            return Ok(sites);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
    }
}
