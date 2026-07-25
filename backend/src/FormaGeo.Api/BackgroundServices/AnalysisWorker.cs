using FormaGeo.Application.Analyses;
using FormaGeo.Infrastructure.Analyses;

namespace FormaGeo.Api.BackgroundServices;

public sealed class AnalysisWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalysisWorker> _logger;
    private readonly TimeSpan _pollingInterval;

    public AnalysisWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AnalysisWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = TimeSpan.FromSeconds(
            Math.Max(
                1,
                configuration.GetValue(
                    "Geoprocessing:PollingIntervalSeconds",
                    2)));
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RecoverInterruptedRunsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedRun = false;

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();
                var processor =
                    scope.ServiceProvider
                        .GetRequiredService<AnalysisRunProcessor>();
                processedRun = await processor.ProcessNextAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "The analysis worker could not inspect the queue.");
            }

            if (!processedRun)
            {
                await Task.Delay(
                    _pollingInterval,
                    stoppingToken);
            }
        }
    }

    private async Task RecoverInterruptedRunsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();
            var repository =
                scope.ServiceProvider
                    .GetRequiredService<IAnalysisRunRepository>();
            await repository.RecoverInterruptedAsync(
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Interrupted analysis runs could not be recovered.");
        }
    }
}
