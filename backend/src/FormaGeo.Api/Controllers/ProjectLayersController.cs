using FormaGeo.Application.Layers;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/layers")]
[Tags("Layers")]
public sealed class ProjectLayersController : ControllerBase
{
    private readonly LayerCatalogService _layerCatalogService;

    public ProjectLayersController(
        LayerCatalogService layerCatalogService)
    {
        _layerCatalogService = layerCatalogService;
    }

    [HttpGet(Name = "GetProjectLayers")]
    [EndpointSummary("Get saved project layer preferences")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ProjectLayerResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectLayerResponse>>>
        GetProjectLayersAsync(
            Guid projectId,
            CancellationToken cancellationToken)
    {
        try
        {
            var layers =
                await _layerCatalogService.GetProjectLayersAsync(
                    projectId,
                    cancellationToken);

            return Ok(layers);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
    }

    [HttpPut(Name = "UpdateProjectLayers")]
    [EndpointSummary("Replace saved project layer preferences")]
    [Consumes("application/json")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ProjectLayerResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectLayerResponse>>>
        UpdateProjectLayersAsync(
            Guid projectId,
            UpdateProjectLayersRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var layers =
                await _layerCatalogService.UpdateProjectLayersAsync(
                    projectId,
                    request,
                    cancellationToken);

            return Ok(layers);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }
}
