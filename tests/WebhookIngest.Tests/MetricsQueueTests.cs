using WebhookIngest.Metrics;

namespace WebhookIngest.Tests;

public class MetricsQueueTests
{
    [Fact]
    public async Task Pending_ids_are_coalesced_into_one_batch()
    {
        var queue = new MetricsQueue();
        queue.Enqueue("cmp_a");
        queue.Enqueue("cmp_b");
        queue.Enqueue("cmp_a");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var batches = queue.ReadBatchesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        Assert.True(await batches.MoveNextAsync());
        Assert.Equal(["cmp_a", "cmp_b"], batches.Current.Order());
    }

    [Fact]
    public async Task Reader_waits_for_work_and_stops_on_cancellation()
    {
        var queue = new MetricsQueue();
        using var cts = new CancellationTokenSource();
        var received = new List<IReadOnlyCollection<string>>();

        var reader = Task.Run(async () =>
        {
            await foreach (var batch in queue.ReadBatchesAsync(cts.Token))
            {
                received.Add(batch);
                await cts.CancelAsync();
            }
        });

        queue.Enqueue("cmp_later");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(["cmp_later"], Assert.Single(received));
    }
}
