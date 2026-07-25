using FormaGeo.Application.Projects.CreateProject;
using FormaGeo.Application.Projects.GetProject;
using FormaGeo.Application.Projects.GetProjects;
using FormaGeo.Application.Sites.CreateSite;
using FormaGeo.Application.Sites.DeleteSite;
using FormaGeo.Application.Sites.GetProjectSites;
using FormaGeo.Application.Sites.GetSite;
using FormaGeo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
app.MapControllers();
app.Run();
