namespace WebhookIngest.Signatures;

/// <summary>Request feature set by <see cref="SvixSignatureFilter"/> once a request is authenticated.</summary>
public sealed record VerifiedWebhook(string MessageId, byte[] Body);
