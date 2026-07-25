using FormaGeo.Application.Projects;
using FormaGeo.Application.Sites;
using FormaGeo.Application.Sites.Summaries;
using FormaGeo.Infrastructure.Persistence;
using FormaGeo.Infrastructure.Persistence.Queries;
using FormaGeo.Infrastructure.Persistence.Repositories;
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
            ISiteRepository,
            SiteRepository>();

        services.AddScoped<
            ISiteSummaryReader,
            PostGisSiteSummaryReader>();

        return services;
    }
}
