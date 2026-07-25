using FormaGeo.Domain.Projects;

namespace FormaGeo.Application.Projects;

public interface IProjectRepository
{
    Task AddAsync(
        Project project,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
