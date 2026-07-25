using System.ComponentModel.DataAnnotations;
using FormaGeo.Application.SiteImports;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

public sealed class SiteImportForm
{
    [Required]
    public IFormFile? File { get; init; }

    public string? NamePattern { get; init; }

    public SiteImportMode Mode { get; init; } =
        SiteImportMode.Separate;

    public List<int>? SelectedFeatureIndexes { get; init; }
}

[ApiController]
[Route("api/projects/{projectId:guid}/site-imports")]
[Tags("Site imports")]
public sealed class SiteImportsController : ControllerBase
{
    private readonly SiteImportService _siteImportService;

    public SiteImportsController(
        SiteImportService siteImportService)
    {
        _siteImportService = siteImportService;
    }

    [HttpPost(Name = "ImportProjectSites")]
    [EndpointSummary("Import Sites from GeoJSON")]
    [EndpointDescription(
        "Synchronously validates a GeoJSON file and imports selected Polygon or MultiPolygon features as Sites.")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(SiteImportResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status413PayloadTooLarge)]
    [RequestSizeLimit(1_150_000)]
    public async Task<ActionResult<SiteImportResult>>
        ImportSitesAsync(
            Guid projectId,
            [FromForm] SiteImportForm form,
            CancellationToken cancellationToken)
    {
        if (form.File is null)
        {
            return ImportError(
                "file",
                "file_required",
                "Choose a GeoJSON file to import.");
        }

        if (form.File.Length >
            SiteImportService.MaximumFileSizeBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new
                {
                    field = "file",
                    problem = "file_too_large",
                    message =
                        "The GeoJSON file cannot exceed 1 MB.",
                    status =
                        StatusCodes.Status413PayloadTooLarge
                });
        }

        try
        {
            await using var stream =
                form.File.OpenReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(
                cancellationToken);
            var result =
                await _siteImportService.HandleAsync(
                    projectId,
                    new SiteImportRequest(
                        form.File.FileName,
                        content,
                        form.NamePattern,
                        form.Mode,
                        form.SelectedFeatureIndexes),
                    cancellationToken);

            return Ok(result);
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
        catch (SiteImportException exception)
        {
            return ImportError(
                exception.Field,
                exception.Problem,
                exception.Message);
        }
        catch (InvalidDataException)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new
                {
                    field = "file",
                    problem = "file_too_large",
                    message =
                        "The GeoJSON file cannot exceed 1 MB.",
                    status =
                        StatusCodes.Status413PayloadTooLarge
                });
        }
    }

    private BadRequestObjectResult ImportError(
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
