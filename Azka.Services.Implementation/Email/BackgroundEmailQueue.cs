using System.Threading.Channels;

namespace Azka.Services.Implementation.Email;

/// <summary>
/// A bounded in-memory channel that queues <see cref="EmailJobDescriptor"/> records.
/// HTTP-thread services write here and return immediately.
/// <see cref="EmailSenderBackgroundService"/> reads and sends off the HTTP thread.
/// </summary>
public sealed class BackgroundEmailQueue
{
    private readonly Channel<EmailJobDescriptor> _queue =
        Channel.CreateBounded<EmailJobDescriptor>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public async ValueTask EnqueueAsync(EmailJobDescriptor job,
        CancellationToken cancellationToken = default)
        => await _queue.Writer.WriteAsync(job, cancellationToken);

    public async ValueTask<EmailJobDescriptor> DequeueAsync(
        CancellationToken cancellationToken)
        => await _queue.Reader.ReadAsync(cancellationToken);
}
