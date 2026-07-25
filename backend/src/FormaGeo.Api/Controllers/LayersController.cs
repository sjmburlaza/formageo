using FormaGeo.Application.Layers;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/layers")]
[Tags("Layers")]
public sealed class LayersController : ControllerBase
{
    private readonly LayerCatalogService _layerCatalogService;

    public LayersController(
        LayerCatalogService layerCatalogService)
    {
        _layerCatalogService = layerCatalogService;
    }

    [HttpGet(Name = "GetLayers")]
    [EndpointSummary("List the analysis layer catalog")]
    [ProducesResponseType(
        typeof(IReadOnlyList<LayerResponse>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LayerResponse>>>
        GetLayersAsync(
            CancellationToken cancellationToken)
    {
        var layers = await _layerCatalogService.GetCatalogAsync(
            cancellationToken);

        return Ok(layers);
    }

    [HttpGet("{layerId:guid}", Name = "GetLayer")]
    [EndpointSummary("Get layer metadata")]
    [ProducesResponseType(
        typeof(LayerResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LayerResponse>> GetLayerAsync(
        Guid layerId,
        CancellationToken cancellationToken)
    {
        var layer = await _layerCatalogService.GetLayerAsync(
            layerId,
            cancellationToken);

        return layer is null
            ? NotFound(new
            {
                error = $"Layer '{layerId}' was not found."
            })
            : Ok(layer);
    }

    [HttpGet("{layerId:guid}/legend", Name = "GetLayerLegend")]
    [EndpointSummary("Get a layer legend")]
    [ProducesResponseType(
        typeof(LayerLegendResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LayerLegendResponse>>
        GetLayerLegendAsync(
            Guid layerId,
            CancellationToken cancellationToken)
    {
        var legend = await _layerCatalogService.GetLegendAsync(
            layerId,
            cancellationToken);

        return legend is null
            ? NotFound(new
            {
                error = $"Layer '{layerId}' was not found."
            })
            : Ok(legend);
    }
}
