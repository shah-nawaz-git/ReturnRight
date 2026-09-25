namespace ReturnRight.Api.Features.Intakes;

public class IntakeCleanupWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<IntakeCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(
            configuration.GetValue("Intakes:CleanupIntervalMinutes", 15));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = services.CreateAsyncScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IntakeCleanupService>();
                var removed = await cleanup.CleanupExpiredAsync(stoppingToken);
                if (removed > 0)
                {
                    logger.LogInformation("Cleaned up {Count} expired intakes", removed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Intake cleanup pass failed");
            }
        }
    }
}
