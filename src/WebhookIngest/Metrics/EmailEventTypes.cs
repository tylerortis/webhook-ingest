namespace WebhookIngest.Metrics;

public static class EmailEventTypes
{
    public const string Sent = "email.sent";
    public const string Delivered = "email.delivered";
    public const string Opened = "email.opened";
    public const string Clicked = "email.clicked";
    public const string Bounced = "email.bounced";
    public const string Complained = "email.complained";
    public const string Unsubscribed = "email.unsubscribed";
}
