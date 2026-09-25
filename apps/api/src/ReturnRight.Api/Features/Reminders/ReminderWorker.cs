using Microsoft.Extensions.Options;

namespace ReturnRight.Api.Features.Reminders;

public class ReminderWorker(
    IServiceProvider services,
    IOptions<ReminderOptions> options,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = services.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ReminderProcessor>();
                var result = await processor.ProcessDueAsync(stoppingToken);
                if (result.Processed > 0)
                {
                    logger.LogInformation(
                        "Processed {Processed} reminders ({Sent} sent, {Failed} failed, {Cancelled} cancelled)",
                        result.Processed, result.Sent, result.Failed, result.Cancelled);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Reminder processing pass failed");
            }
        }
    }
}
