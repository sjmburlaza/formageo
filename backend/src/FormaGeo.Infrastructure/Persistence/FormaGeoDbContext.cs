using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence;

public sealed class FormaGeoDbContext : DbContext
{
    public FormaGeoDbContext(
        DbContextOptions<FormaGeoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Site> Sites => Set<Site>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FormaGeoDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}