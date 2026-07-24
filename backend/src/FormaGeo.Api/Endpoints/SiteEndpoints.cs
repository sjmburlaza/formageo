using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.CreateSite;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.GetProjectSites;
using FormaGeo.Application.Sites.GetSite;

namespace FormaGeo.Api.Endpoints;

public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var projectSites = endpoints
            .MapGroup(
                "/api/projects/{projectId:guid}/sites")
            .WithTags("Sites");

        projectSites
            .MapPost("", CreateSiteAsync)
            .WithName("CreateSite")
            .WithSummary("Create a site")
            .WithDescription(
                "Creates a site with a GeoJSON Polygon boundary.")
            .Accepts<CreateSiteRequest>("application/json")
            .Produces<SiteResponse>(
                StatusCodes.Status201Created)
            .Produces(
                StatusCodes.Status400BadRequest)
            .Produces(
                StatusCodes.Status404NotFound);

        projectSites
            .MapGet("", GetProjectSitesAsync)
            .WithName("GetProjectSites")
            .WithSummary("List project sites")
            .WithDescription(
                "Returns every site belonging to a project.")
            .Produces<SiteResponse[]>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound);

        var sites = endpoints
            .MapGroup("/api/sites")
            .WithTags("Sites");

        sites
            .MapGet("/{siteId:guid}", GetSiteAsync)
            .WithName("GetSite")
            .WithSummary("Get a site")
            .WithDescription(
                "Returns one site and its GeoJSON boundary.")
            .Produces<SiteResponse>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound);

        sites
            .MapDelete("/{siteId:guid}", DeleteSiteAsync)
            .WithName("DeleteSite")
            .WithSummary("Delete a site")
            .WithDescription(
                "Permanently deletes a site.")
            .Produces(
                StatusCodes.Status204NoContent)
            .Produces(
                StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateSiteAsync(
        Guid projectId,
        CreateSiteRequest request,
        CreateSiteHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await handler.HandleAsync(
                projectId,
                request,
                cancellationToken);

            return Results.Created(
                $"/api/sites/{site.Id}",
                site);
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new
            {
                error = exception.Message
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    private static async Task<IResult>
        GetProjectSitesAsync(
            Guid projectId,
            GetProjectSitesHandler handler,
            CancellationToken cancellationToken)
    {
        try
        {
            var sites = await handler.HandleAsync(
                projectId,
                cancellationToken);

            return Results.Ok(sites);
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new
            {
                error = exception.Message
            });
        }
    }

    private static async Task<IResult> GetSiteAsync(
        Guid siteId,
        GetSiteHandler handler,
        CancellationToken cancellationToken)
    {
        var site = await handler.HandleAsync(
            siteId,
            cancellationToken);

        if (site is null)
        {
            return Results.NotFound(new
            {
                error = $"Site '{siteId}' was not found."
            });
        }

        return Results.Ok(site);
    }

    private static async Task<IResult> DeleteSiteAsync(
        Guid siteId,
        DeleteSiteHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.HandleAsync(
            siteId,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound(new
            {
                error = $"Site '{siteId}' was not found."
            });
        }

        return Results.NoContent();
    }
}