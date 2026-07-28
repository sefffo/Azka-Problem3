using Azka.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azka.Services.Implementation.Email;

/// <summary>
/// Long-running worker that drains <see cref="BackgroundEmailQueue"/>.
/// Creates a fresh DI scope per job so it resolves a new scoped
/// <see cref="IEmailService"/> — avoids ObjectDisposedException.
/// </summary>
public sealed class EmailSenderBackgroundService(
    BackgroundEmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailSenderBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[EmailWorker] Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await queue.DequeueAsync(stoppingToken);

                logger.LogInformation("[EmailWorker] Sending '{Subject}' → {To}",
                    job.Subject, job.To);

                // Each job gets its own scope → fresh IEmailService instance
                await using var scope = scopeFactory.CreateAsyncScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                await emailService.SendAsync(job.To, job.Subject, job.Body, job.IsHtml);

                logger.LogInformation("[EmailWorker] Sent successfully → {To}", job.To);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[EmailWorker] Shutdown requested.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[EmailWorker] Failed to send email. {ExType}: {Message}",
                    ex.GetType().Name, ex.Message);
            }
        }

        logger.LogInformation("[EmailWorker] Stopped.");
    }
}
