using FormaGeo.Application.Projects.CreateProject;
using FormaGeo.Application.Projects.GetProject;
using FormaGeo.Application.Projects.GetProjects;
using FormaGeo.Application.Sites.CreateSite;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.GetProjectSites;
using FormaGeo.Application.Sites.GetSite;
using FormaGeo.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

const long MaximumRequestBodySize = 1_048_576;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        MaximumRequestBodySize;
});

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState
                .SelectMany(entry => entry.Value?.Errors
                    .Select(error => new
                    {
                        Field = string.IsNullOrWhiteSpace(entry.Key)
                            ? "request"
                            : entry.Key,
                        Error = error
                    }) ?? [])
                .FirstOrDefault();
            var message = firstError?.Error.ErrorMessage;

            if (string.IsNullOrWhiteSpace(message))
            {
                message =
                    "The request body is not valid JSON or does not match the expected site format.";
            }

            return new BadRequestObjectResult(new
            {
                field = firstError?.Field ?? "request",
                problem = "invalid_request",
                message,
                status = StatusCodes.Status400BadRequest
            });
        };
    });
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddScoped<CreateProjectHandler>();
builder.Services.AddScoped<GetProjectHandler>();
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
            .WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200")
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
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;

    if (response.StatusCode !=
        StatusCodes.Status413PayloadTooLarge)
    {
        return;
    }

    response.ContentType = "application/json";
    await response.WriteAsJsonAsync(new
    {
        field = "request",
        problem = "request_too_large",
        message = "The request body cannot exceed 1 MB.",
        status = StatusCodes.Status413PayloadTooLarge
    });
});
app.MapControllers();
app.Run();
