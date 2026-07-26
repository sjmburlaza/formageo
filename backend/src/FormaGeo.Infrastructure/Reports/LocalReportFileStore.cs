using FormaGeo.Application.Reports;
using Microsoft.Extensions.Options;

namespace FormaGeo.Infrastructure.Reports;

public sealed class LocalReportFileStore : IReportFileStore
{
    private readonly string _rootPath;

    public LocalReportFileStore(
        IOptions<ReportStorageOptions> options)
    {
        var configuredPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "Report storage root path is required.");
        }

        _rootPath = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredReportFile> SaveAsync(
        Guid reportId,
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty)
        {
            throw new ArgumentException(
                "A report ID is required.",
                nameof(reportId));
        }

        var extension = Path.GetExtension(fileName);
        var storageKey = Path.Combine(
            reportId.ToString("N")[..2],
            $"{reportId:N}{extension.ToLowerInvariant()}");
        var destinationPath = Resolve(storageKey);
        var directory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{reportId:N}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(
                temporaryPath,
                content.ToArray(),
                cancellationToken);
            File.Move(
                temporaryPath,
                destinationPath,
                true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new StoredReportFile(
            storageKey.Replace(
                Path.DirectorySeparatorChar,
                '/'),
            content.Length);
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);

        return Task.FromResult<Stream?>(
            File.Exists(path)
                ? new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan)
                : null);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        var normalizedKey = storageKey.Replace(
            '/',
            Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(
            Path.Combine(
                _rootPath,
                normalizedKey));
        var rootPrefix = _rootPath.EndsWith(
            Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The report storage key is outside the configured root.");
        }

        return fullPath;
    }
}
