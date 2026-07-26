using FormaGeo.Domain.Analyses;
using FormaGeo.Domain.Comparisons;
using FormaGeo.Domain.Layers;
using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Scoring;
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

    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();

    public DbSet<SiteComparison> SiteComparisons =>
        Set<SiteComparison>();

    public DbSet<DataSource> DataSources => Set<DataSource>();

    public DbSet<LayerDefinition> LayerDefinitions =>
        Set<LayerDefinition>();

    public DbSet<LayerVersion> LayerVersions => Set<LayerVersion>();

    public DbSet<ProjectLayer> ProjectLayers => Set<ProjectLayer>();

    public DbSet<LayerLegend> LayerLegends => Set<LayerLegend>();

    public DbSet<ScoringScenario> ScoringScenarios =>
        Set<ScoringScenario>();

    public DbSet<ScoringModel> ScoringModels =>
        Set<ScoringModel>();

    public DbSet<ScoringCriterion> ScoringCriteria =>
        Set<ScoringCriterion>();

    public DbSet<ScoringResult> ScoringResults =>
        Set<ScoringResult>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FormaGeoDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
