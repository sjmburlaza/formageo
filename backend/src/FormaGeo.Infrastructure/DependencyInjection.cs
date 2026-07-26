using FormaGeo.Application.Analyses;
using FormaGeo.Application.Comparisons;
using FormaGeo.Application.Layers;
using FormaGeo.Application.Projects;
using FormaGeo.Application.Scoring;
using FormaGeo.Application.Sites;
using FormaGeo.Application.Sites.Summaries;
using FormaGeo.Infrastructure.Analyses;
using FormaGeo.Infrastructure.Persistence;
using FormaGeo.Infrastructure.Persistence.Queries;
using FormaGeo.Infrastructure.Persistence.Repositories;
using FormaGeo.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FormaGeo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "The Database connection string is missing.");

        services.AddDbContext<FormaGeoDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                });
        });

        services.AddScoped<
            IProjectRepository,
            ProjectRepository>();

        services.AddScoped<
            ILayerCatalogRepository,
            LayerCatalogRepository>();

        services.AddScoped<
            ISiteRepository,
            SiteRepository>();

        services.AddScoped<
            ISiteSummaryReader,
            PostGisSiteSummaryReader>();

        services.AddScoped<
            IAnalysisRunRepository,
            AnalysisRunRepository>();

        services.AddScoped<
            IScoringRepository,
            ScoringRepository>();
        services.AddScoped<
            IScoringValueProvider,
            AnalysisScoringValueProvider>();
        services.AddScoped<
            IComparisonRepository,
            ComparisonRepository>();

        services.AddScoped<AnalysisRunProcessor>();
        services.AddScoped<DevelopmentMockDataSeeder>();

        var geoprocessingBaseUrl =
            configuration["Geoprocessing:BaseUrl"]
            ?? "http://localhost:8000/";

        if (!geoprocessingBaseUrl.EndsWith('/'))
        {
            geoprocessingBaseUrl += "/";
        }

        services.AddSingleton<IAnalysisExecutor>(
            new GeoprocessingAnalysisExecutor(
                geoprocessingBaseUrl));

        return services;
    }
}
