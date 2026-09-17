using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace WebhookIngest.Metrics;

public interface IMetricsQueue
{
    void Enqueue(string campaignId);

    IAsyncEnumerable<IReadOnlyCollection<string>> ReadBatchesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// In-process hand-off from ingestion to the recomputer. Everything waiting when the reader
/// wakes up becomes one batch of distinct campaign ids, so a burst of events for the same
/// campaign triggers a single recompute.
/// </summary>
public sealed class MetricsQueue : IMetricsQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(string campaignId) => _channel.Writer.TryWrite(campaignId);

    public async IAsyncEnumerable<IReadOnlyCollection<string>> ReadBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            var batch = new HashSet<string>(StringComparer.Ordinal);
            while (_channel.Reader.TryRead(out var campaignId))
            {
                batch.Add(campaignId);
            }

            yield return batch;
        }
    }
}
