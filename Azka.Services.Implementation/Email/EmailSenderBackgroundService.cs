using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azka.Services.Implementation.Email;

/// <summary>
/// A long-running <see cref="BackgroundService"/> that continuously drains
/// the <see cref="BackgroundEmailQueue"/> and executes each email job.
/// Decoupled entirely from the HTTP request pipeline — SMTP latency never
/// blocks a controller response.
/// </summary>
public class EmailSenderBackgroundService(
    BackgroundEmailQueue queue,
    ILogger<EmailSenderBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[EmailWorker] Background email sender started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Blocks here — zero CPU usage until a job is written to the channel.
                var job = await queue.DequeueAsync(stoppingToken);

                logger.LogInformation("[EmailWorker] Picked up email job, executing...");
                await job(stoppingToken);
                logger.LogInformation("[EmailWorker] Email job completed successfully.");
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[EmailWorker] Shutdown requested, stopping email worker.");
                break;
            }
            catch (Exception ex)
            {
                // Log full details but do NOT rethrow — that would kill the worker permanently.
                logger.LogError(ex,
                    "[EmailWorker] FAILED to send email. ExType: {ExType} | Message: {ExMessage}",
                    ex.GetType().Name, ex.Message);
            }
        }

        logger.LogInformation("[EmailWorker] Background email sender stopped.");
    }
}
