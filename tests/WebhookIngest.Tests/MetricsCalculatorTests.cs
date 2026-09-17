using WebhookIngest.Metrics;

namespace WebhookIngest.Tests;

public class MetricsCalculatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 2, 14, 0, 0, TimeSpan.Zero);

    private static EventFact At(string type, string recipient, int minutes) => new(type, recipient, T0.AddMinutes(minutes));

    private static List<EventFact> Scenario() =>
    [
        At(EmailEventTypes.Sent, "ada@example.com", 0),
        At(EmailEventTypes.Sent, "grace@example.com", 0),
        At(EmailEventTypes.Sent, "linus@example.com", 0),
        At(EmailEventTypes.Delivered, "ada@example.com", 1),
        At(EmailEventTypes.Delivered, "grace@example.com", 1),
        At(EmailEventTypes.Bounced, "linus@example.com", 1),
        At(EmailEventTypes.Opened, "ada@example.com", 10),
        At(EmailEventTypes.Opened, "ada@example.com", 40),
        At(EmailEventTypes.Opened, "grace@example.com", 12),
        At(EmailEventTypes.Clicked, "ada@example.com", 11),
        At(EmailEventTypes.Clicked, "ada@example.com", 41),
        At(EmailEventTypes.Complained, "grace@example.com", 60),
        At(EmailEventTypes.Unsubscribed, "grace@example.com", 61),
    ];

    [Fact]
    public void Counts_each_metric_by_distinct_recipient()
    {
        var snapshot = MetricsCalculator.Compute(Scenario());

        Assert.Equal(new MetricsSnapshot(
            Sent: 3, Delivered: 2, UniqueOpens: 2, TotalOpens: 3, UniqueClicks: 1, TotalClicks: 2,
            Bounced: 1, Complained: 1, Unsubscribed: 1, LastEventAt: T0.AddMinutes(61)), snapshot);
    }

    [Fact]
    public void Repeated_events_for_the_same_recipient_do_not_inflate_unique_counts()
    {
        var events = new[]
        {
            At(EmailEventTypes.Delivered, "ada@example.com", 1),
            At(EmailEventTypes.Delivered, "ada@example.com", 2),
            At(EmailEventTypes.Opened, "ada@example.com", 3),
            At(EmailEventTypes.Opened, "ADA@example.com", 4),
        };

        var snapshot = MetricsCalculator.Compute(events);

        Assert.Equal(1, snapshot.Delivered);
        Assert.Equal(1, snapshot.UniqueOpens);
        Assert.Equal(2, snapshot.TotalOpens);
    }

    [Fact]
    public void Result_does_not_depend_on_event_order()
    {
        var inOrder = MetricsCalculator.Compute(Scenario());
        var reversed = Scenario();
        reversed.Reverse();
        var shuffled = Scenario().OrderBy(e => e.Recipient).ThenByDescending(e => e.Type).ToList();

        Assert.Equal(inOrder, MetricsCalculator.Compute(reversed));
        Assert.Equal(inOrder, MetricsCalculator.Compute(shuffled));
    }

    [Fact]
    public void Unknown_event_types_are_ignored_for_counts_but_still_move_last_event_time()
    {
        var events = new[]
        {
            At(EmailEventTypes.Delivered, "ada@example.com", 1),
            At("email.delivery_delayed", "grace@example.com", 5),
        };

        var snapshot = MetricsCalculator.Compute(events);

        Assert.Equal(1, snapshot.Delivered);
        Assert.Equal(0, snapshot.Sent);
        Assert.Equal(T0.AddMinutes(5), snapshot.LastEventAt);
    }

    [Fact]
    public void No_events_yields_zeros_and_no_last_event_time()
    {
        Assert.Equal(new MetricsSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, null), MetricsCalculator.Compute([]));
    }
}
