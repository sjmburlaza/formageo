using FormaGeo.Application.Analyses;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Analyses")]
public sealed class AnalysesController : ControllerBase
{
    private readonly AnalysisRunService _analysisRunService;

    public AnalysesController(
        AnalysisRunService analysisRunService)
    {
        _analysisRunService = analysisRunService;
    }

    [HttpGet("analyses/catalog", Name = "GetAnalysisCatalog")]
    [EndpointSummary("List available analyses")]
    [ProducesResponseType(
        typeof(IReadOnlyList<AnalysisDefinitionResponse>),
        StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AnalysisDefinitionResponse>>
        GetCatalog()
    {
        return Ok(AnalysisCatalog.GetAll());
    }

    [HttpPost(
        "sites/{siteId:guid}/analyses",
        Name = "CreateAnalysis")]
    [EndpointSummary("Request a site analysis")]
    [EndpointDescription(
        "Stores a pending analysis run. Processing continues asynchronously.")]
    [ProducesResponseType(
        typeof(AnalysisRunResponse),
        StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisRunResponse>>
        CreateAsync(
            Guid siteId,
            CreateAnalysisRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var analysisRun =
                await _analysisRunService.CreateAsync(
                    siteId,
                    request,
                    cancellationToken);

            return AcceptedAtRoute(
                "GetAnalysis",
                new { analysisId = analysisRun.Id },
                analysisRun);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (AnalysisValidationException exception)
        {
            return BadRequest(new
            {
                field = exception.Field,
                problem = "validation_error",
                message = exception.Message,
                status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet(
        "analyses/{analysisId:guid}",
        Name = "GetAnalysis")]
    [EndpointSummary("Get an analysis run")]
    [ProducesResponseType(
        typeof(AnalysisRunResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisRunResponse>>
        GetAsync(
            Guid analysisId,
            CancellationToken cancellationToken)
    {
        var analysisRun =
            await _analysisRunService.GetAsync(
                analysisId,
                cancellationToken);

        return analysisRun is null
            ? NotFound(new
            {
                error =
                    $"Analysis run '{analysisId}' was not found."
            })
            : Ok(analysisRun);
    }

    [HttpGet(
        "sites/{siteId:guid}/analyses",
        Name = "GetSiteAnalyses")]
    [EndpointSummary("List analysis history for a site")]
    [ProducesResponseType(
        typeof(IReadOnlyList<AnalysisRunResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<AnalysisRunResponse>>>
        GetForSiteAsync(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _analysisRunService.GetForSiteAsync(
                    siteId,
                    cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
    }

    [HttpDelete(
        "analyses/{analysisId:guid}",
        Name = "CancelAnalysis")]
    [EndpointSummary("Cancel a pending or running analysis")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        try
        {
            var cancelled =
                await _analysisRunService.CancelAsync(
                    analysisId,
                    cancellationToken);

            return cancelled
                ? NoContent()
                : Conflict(new
                {
                    error =
                        "Only pending or running analyses can be cancelled."
                });
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
