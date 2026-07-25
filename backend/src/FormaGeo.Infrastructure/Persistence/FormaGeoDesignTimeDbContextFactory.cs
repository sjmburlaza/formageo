using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FormaGeo.Infrastructure.Persistence;

public sealed class FormaGeoDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<FormaGeoDbContext>
{
    public FormaGeoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<FormaGeoDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5433;Database=formageo;Username=formageo;Password=formageo_dev",
            npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
            });

        return new FormaGeoDbContext(optionsBuilder.Options);
    }
}
