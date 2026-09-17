namespace WebhookIngest.Signatures;

/// <summary>
/// Reads the raw body (the signature covers exact bytes, so it must be verified before any
/// JSON parsing) and short-circuits with 401 when verification fails. The response never
/// says which check failed; the reason is logged server-side only.
/// </summary>
public sealed class SvixSignatureFilter(SvixSignatureVerifier verifier, ILogger<SvixSignatureFilter> logger) : IEndpointFilter
{
    public const int MaxBodyBytes = 256 * 1024;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var body = await ReadBodyAsync(http.Request, http.RequestAborted);
        if (body is null)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var headers = http.Request.Headers;
        string? messageId = headers["svix-id"];
        var result = verifier.Verify(messageId, headers["svix-timestamp"], headers["svix-signature"], body);

        if (result != SignatureCheck.Valid)
        {
            logger.LogWarning("Rejected webhook {MessageId}: {Reason}", messageId, result);
            return Results.Unauthorized();
        }

        http.Features.Set(new VerifiedWebhook(messageId!, body));
        return await next(context);
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
