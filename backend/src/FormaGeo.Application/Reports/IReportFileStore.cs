namespace FormaGeo.Application.Reports;

public interface IReportFileStore
{
    Task<StoredReportFile> SaveAsync(
        Guid reportId,
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
