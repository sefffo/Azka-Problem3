using System.Threading.Channels;

namespace Azka.Services.Implementation.Email;

/// <summary>
/// An in-memory bounded channel that acts as a fire-and-forget queue.
/// Any service drops an email job here and returns immediately —
/// the <see cref="EmailSenderBackgroundService"/> picks it up off the HTTP thread.
/// </summary>
public class BackgroundEmailQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public BackgroundEmailQueue()
    {
        // Capacity of 100 queued emails; back-pressure drops oldest if full.
        _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(100);
    }

    public async ValueTask EnqueueAsync(Func<CancellationToken, Task> job)
        => await _queue.Writer.WriteAsync(job);

    public async ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        => await _queue.Reader.ReadAsync(cancellationToken);
}
