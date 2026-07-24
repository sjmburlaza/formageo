using FormaGeo.Api.Endpoints;
using FormaGeo.Application.Projects.CreateProject;
using FormaGeo.Application.Projects.GetProjects;
using FormaGeo.Application.Sites.CreateSite;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.GetProjectSites;
using FormaGeo.Application.Sites.GetSite;
using FormaGeo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddScoped<CreateProjectHandler>();
builder.Services.AddScoped<GetProjectsHandler>();

builder.Services.AddScoped<CreateSiteHandler>();
builder.Services.AddScoped<GetProjectSitesHandler>();
builder.Services.AddScoped<GetSiteHandler>();
builder.Services.AddScoped<DeleteSiteHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "FormaGeo API v1");

        options.DocumentTitle = "FormaGeo API";
    });
}

app.UseCors("Frontend");

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        service = "FormaGeo.Api",
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithTags("Health")
.WithSummary("Check API health");

app.MapPost(
    "/api/projects",
    async (
        CreateProjectRequest request,
        CreateProjectHandler handler,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var project = await handler.HandleAsync(
                request,
                cancellationToken);

            return Results.Created(
                $"/api/projects/{project.Id}",
                project);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                error = exception.Message
            });
        }
    })
    .WithTags("Projects")
    .WithName("CreateProject")
    .WithSummary("Create a project");

app.MapGet(
    "/api/projects",
    async (
        GetProjectsHandler handler,
        CancellationToken cancellationToken) =>
    {
        var projects = await handler.HandleAsync(
            cancellationToken);

        return Results.Ok(projects);
    })
    .WithTags("Projects")
    .WithName("GetProjects")
    .WithSummary("List all projects");

app.MapSiteEndpoints();

app.Run();