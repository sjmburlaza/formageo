using FormaGeo.Application.Comparisons;
using FormaGeo.Domain.Scoring;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Site comparisons")]
public sealed class ComparisonsController : ControllerBase
{
    private readonly ComparisonService _comparisonService;

    public ComparisonsController(
        ComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
    }

    [HttpPost(
        "projects/{projectId:guid}/comparisons",
        Name = "CreateSiteComparison")]
    [EndpointSummary("Evaluate and save a site comparison")]
    [EndpointDescription(
        "Scores two to five sites with one immutable scenario version and " +
        "stores the ranked metric breakdown as a point-in-time snapshot.")]
    [ProducesResponseType(
        typeof(ComparisonResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComparisonResponse>>
        CreateAsync(
            Guid projectId,
            CreateComparisonRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var response = await _comparisonService.CreateAsync(
                projectId,
                request,
                cancellationToken);

            return CreatedAtRoute(
                "GetSiteComparison",
                new { comparisonId = response.Id },
                response);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (ComparisonValidationException exception)
        {
            return BadRequest(new
            {
                field = exception.Field,
                problem = "validation_error",
                message = exception.Message,
                status = StatusCodes.Status400BadRequest
            });
        }
        catch (ScoringValidationException exception)
        {
            return BadRequest(new
            {
                field = exception.Field,
                problem = "validation_error",
                message = exception.Message,
                status = StatusCodes.Status400BadRequest
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                field = "request",
                problem = "validation_error",
                message = exception.Message,
                status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet(
        "comparisons/{comparisonId:guid}",
        Name = "GetSiteComparison")]
    [EndpointSummary("Get a saved site comparison")]
    [ProducesResponseType(
        typeof(ComparisonResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComparisonResponse>>
        GetAsync(
            Guid comparisonId,
            CancellationToken cancellationToken)
    {
        var response = await _comparisonService.GetAsync(
            comparisonId,
            cancellationToken);

        return response is null
            ? NotFound(new
            {
                error =
                    $"Comparison '{comparisonId}' was not found."
            })
            : Ok(response);
    }

    [HttpGet(
        "projects/{projectId:guid}/comparisons",
        Name = "GetProjectSiteComparisons")]
    [EndpointSummary("List a project's saved site comparisons")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ComparisonSummaryResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<ComparisonSummaryResponse>>>
        GetForProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _comparisonService.GetForProjectAsync(
                    projectId,
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
}
