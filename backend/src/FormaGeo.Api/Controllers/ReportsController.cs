using FormaGeo.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Reports and exports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportsController(
        ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpPost("sites/{siteId:guid}/reports")]
    [EndpointSummary("Generate a site report or export")]
    [ProducesResponseType(
        typeof(ReportResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ReportResponse>> CreateSiteReportAsync(
        Guid siteId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        return CreateAsync(
            () => _reportService.CreateForSiteAsync(
                siteId,
                request,
                cancellationToken));
    }

    [HttpPost("sites/{siteId:guid}/reports/preview")]
    [EndpointSummary("Preview a configured site report")]
    [ProducesResponseType(
        typeof(ReportPreviewResponse),
        StatusCodes.Status200OK)]
    public Task<ActionResult<ReportPreviewResponse>>
        PreviewSiteReportAsync(
            Guid siteId,
            CreateReportRequest request,
            CancellationToken cancellationToken)
    {
        return PreviewAsync(
            () => _reportService.PreviewSiteAsync(
                siteId,
                request,
                cancellationToken));
    }

    [HttpGet("sites/{siteId:guid}/reports")]
    [EndpointSummary("List generated reports for a site")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ReportResponse>),
        StatusCodes.Status200OK)]
    public async Task<
        ActionResult<IReadOnlyList<ReportResponse>>>
        GetSiteReportsAsync(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        return Ok(await _reportService.GetForSiteAsync(
            siteId,
            cancellationToken));
    }

    [HttpPost("comparisons/{comparisonId:guid}/reports")]
    [EndpointSummary("Generate a comparison report or CSV")]
    [ProducesResponseType(
        typeof(ReportResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ReportResponse>>
        CreateComparisonReportAsync(
            Guid comparisonId,
            CreateReportRequest request,
            CancellationToken cancellationToken)
    {
        return CreateAsync(
            () => _reportService.CreateForComparisonAsync(
                comparisonId,
                request,
                cancellationToken));
    }

    [HttpPost(
        "comparisons/{comparisonId:guid}/reports/preview")]
    [EndpointSummary("Preview a configured comparison report")]
    [ProducesResponseType(
        typeof(ReportPreviewResponse),
        StatusCodes.Status200OK)]
    public Task<ActionResult<ReportPreviewResponse>>
        PreviewComparisonReportAsync(
            Guid comparisonId,
            CreateReportRequest request,
            CancellationToken cancellationToken)
    {
        return PreviewAsync(
            () => _reportService.PreviewComparisonAsync(
                comparisonId,
                request,
                cancellationToken));
    }

    [HttpGet("comparisons/{comparisonId:guid}/reports")]
    [EndpointSummary("List generated reports for a comparison")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ReportResponse>),
        StatusCodes.Status200OK)]
    public async Task<
        ActionResult<IReadOnlyList<ReportResponse>>>
        GetComparisonReportsAsync(
            Guid comparisonId,
            CancellationToken cancellationToken)
    {
        return Ok(
            await _reportService.GetForComparisonAsync(
                comparisonId,
                cancellationToken));
    }

    [HttpPost("projects/{projectId:guid}/reports")]
    [EndpointSummary("Generate a project data archive")]
    [ProducesResponseType(
        typeof(ReportResponse),
        StatusCodes.Status201Created)]
    public Task<ActionResult<ReportResponse>>
        CreateProjectArchiveAsync(
            Guid projectId,
            CreateReportRequest request,
            CancellationToken cancellationToken)
    {
        return CreateAsync(
            () => _reportService.CreateProjectArchiveAsync(
                projectId,
                request,
                cancellationToken));
    }

    [HttpGet("projects/{projectId:guid}/reports")]
    [EndpointSummary("List generated reports for a project")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ReportResponse>),
        StatusCodes.Status200OK)]
    public async Task<
        ActionResult<IReadOnlyList<ReportResponse>>>
        GetProjectReportsAsync(
            Guid projectId,
            CancellationToken cancellationToken)
    {
        return Ok(await _reportService.GetForProjectAsync(
            projectId,
            cancellationToken));
    }

    [HttpGet("reports/{reportId:guid}", Name = "GetReport")]
    [EndpointSummary("Get generated report metadata")]
    [ProducesResponseType(
        typeof(ReportResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportResponse>> GetAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await _reportService.GetAsync(
            reportId,
            cancellationToken);

        return report is null
            ? NotFound(new
            {
                error =
                    $"Report '{reportId}' was not found."
            })
            : Ok(report);
    }

    [HttpGet("reports/{reportId:guid}/download")]
    [EndpointSummary("Download a previously generated report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var download = await _reportService.DownloadAsync(
            reportId,
            cancellationToken);
        if (download is null)
        {
            return NotFound(new
            {
                error =
                    $"Report file '{reportId}' was not found."
            });
        }

        return File(
            download.Content,
            download.ContentType,
            download.FileName,
            enableRangeProcessing: true);
    }

    private async Task<ActionResult<ReportResponse>> CreateAsync(
        Func<Task<ReportResponse>> action)
    {
        try
        {
            var report = await action();
            return CreatedAtRoute(
                "GetReport",
                new { reportId = report.Id },
                report);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (ReportValidationException exception)
        {
            return ValidationError(
                exception.Field,
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ValidationError(
                exception.ParamName ?? "request",
                exception.Message);
        }
    }

    private async Task<ActionResult<ReportPreviewResponse>>
        PreviewAsync(
            Func<Task<ReportPreviewResponse>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
        }
        catch (ReportValidationException exception)
        {
            return ValidationError(
                exception.Field,
                exception.Message);
        }
    }

    private BadRequestObjectResult ValidationError(
        string field,
        string message)
    {
        return BadRequest(new
        {
            field,
            problem = "validation_error",
            message,
            status = StatusCodes.Status400BadRequest
        });
    }
}
