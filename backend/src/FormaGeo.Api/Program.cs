using FormaGeo.Application.Projects.CreateProject;
using FormaGeo.Application.Projects.GetProjects;
using FormaGeo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<CreateProjectHandler>();
builder.Services.AddScoped<GetProjectsHandler>();

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
});

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
    });

app.MapGet(
    "/api/projects",
    async (
        GetProjectsHandler handler,
        CancellationToken cancellationToken) =>
    {
        var projects = await handler.HandleAsync(
            cancellationToken);

        return Results.Ok(projects);
    });

app.Run();