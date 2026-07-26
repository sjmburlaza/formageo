using FormaGeo.Application.Scoring;
using FormaGeo.Domain.Scoring;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Suitability scoring")]
public sealed class ScoringController : ControllerBase
{
    private readonly ScoringService _scoringService;

    public ScoringController(
        ScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    [HttpGet(
        "scoring/catalog",
        Name = "GetScoringCatalog")]
    [EndpointSummary("List scoring criteria and presets")]
    [ProducesResponseType(
        typeof(ScoringCatalogResponse),
        StatusCodes.Status200OK)]
    public ActionResult<ScoringCatalogResponse> GetCatalog()
    {
        return Ok(ScoringCatalog.Get());
    }

    [HttpGet(
        "projects/{projectId:guid}/scoring-scenarios",
        Name = "GetProjectScoringScenarios")]
    [EndpointSummary("List a project's scoring scenarios")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ScoringScenarioResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<ScoringScenarioResponse>>>
        GetForProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _scoringService.GetForProjectAsync(
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

    [HttpGet(
        "scoring-scenarios/{scenarioId:guid}",
        Name = "GetScoringScenario")]
    [EndpointSummary("Get the latest version of a scoring scenario")]
    [ProducesResponseType(
        typeof(ScoringScenarioResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScoringScenarioResponse>>
        GetAsync(
            Guid scenarioId,
            CancellationToken cancellationToken)
    {
        var response = await _scoringService.GetAsync(
            scenarioId,
            cancellationToken);

        return response is null
            ? NotFound(new
            {
                error =
                    $"Scoring scenario '{scenarioId}' was not found."
            })
            : Ok(response);
    }

    [HttpPost(
        "projects/{projectId:guid}/scoring-scenarios",
        Name = "CreateScoringScenario")]
    [EndpointSummary("Create and version a scoring scenario")]
    [ProducesResponseType(
        typeof(ScoringScenarioResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScoringScenarioResponse>>
        CreateAsync(
            Guid projectId,
            SaveScoringScenarioRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var response = await _scoringService.CreateAsync(
                projectId,
                request,
                cancellationToken);

            return CreatedAtRoute(
                "GetScoringScenario",
                new { scenarioId = response.Id },
                response);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (Exception exception)
            when (exception is ScoringValidationException
                  or ArgumentException)
        {
            return ValidationProblem(exception);
        }
    }

    [HttpPut(
        "scoring-scenarios/{scenarioId:guid}",
        Name = "UpdateScoringScenario")]
    [EndpointSummary("Save a new immutable scoring-model version")]
    [ProducesResponseType(
        typeof(ScoringScenarioResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScoringScenarioResponse>>
        UpdateAsync(
            Guid scenarioId,
            SaveScoringScenarioRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _scoringService.UpdateAsync(
                    scenarioId,
                    request,
                    cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (Exception exception)
            when (exception is ScoringValidationException
                  or ArgumentException)
        {
            return ValidationProblem(exception);
        }
    }

    [HttpPost(
        "scoring-scenarios/{scenarioId:guid}/duplicate",
        Name = "DuplicateScoringScenario")]
    [EndpointSummary("Duplicate a scoring scenario")]
    [ProducesResponseType(
        typeof(ScoringScenarioResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScoringScenarioResponse>>
        DuplicateAsync(
            Guid scenarioId,
            CancellationToken cancellationToken)
    {
        try
        {
            var response =
                await _scoringService.DuplicateAsync(
                    scenarioId,
                    cancellationToken);

            return CreatedAtRoute(
                "GetScoringScenario",
                new { scenarioId = response.Id },
                response);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
    }

    [HttpPost(
        "scoring-scenarios/{scenarioId:guid}/score",
        Name = "RunSuitabilityScoring")]
    [EndpointSummary("Score and compare sites with a saved model version")]
    [EndpointDescription(
        "Returns the normalized value, effective weight, contribution, " +
        "data version, and missing-data handling for every criterion.")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ScoringResultResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<ScoringResultResponse>>>
        RunAsync(
            Guid scenarioId,
            RunScoringRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _scoringService.RunAsync(
                    scenarioId,
                    request,
                    cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (Exception exception)
            when (exception is ScoringValidationException
                  or ArgumentException)
        {
            return ValidationProblem(exception);
        }
    }

    [HttpGet(
        "scoring-scenarios/{scenarioId:guid}/results",
        Name = "GetScoringResults")]
    [EndpointSummary("List saved score breakdowns for comparison")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ScoringResultResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<ScoringResultResponse>>>
        GetResultsAsync(
            Guid scenarioId,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _scoringService.GetResultsAsync(
                    scenarioId,
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

    private BadRequestObjectResult ValidationProblem(
        Exception exception)
    {
        var field = exception is ScoringValidationException validation
            ? validation.Field
            : "criteria";

        return BadRequest(new
        {
            field,
            problem = "validation_error",
            message = exception.Message,
            status = StatusCodes.Status400BadRequest
        });
    }
}
