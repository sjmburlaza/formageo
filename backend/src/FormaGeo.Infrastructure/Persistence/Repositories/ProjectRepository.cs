using FormaGeo.Application.Projects;
using FormaGeo.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository
    : IProjectRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public ProjectRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Projects.Add(project);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<Project>>
        GetAllAsync(
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .OrderByDescending(project =>
                project.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                project => project.Id == projectId,
                cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                project => project.Id == projectId,
                cancellationToken);
    }
}
