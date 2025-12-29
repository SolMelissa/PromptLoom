#if NET8_0_OR_GREATER
// Build note (PromptLoom integration):
// - Error CS0246: IEndpointRouteBuilder could not be found
// - Fix: add Microsoft.AspNetCore.Routing using to resolve the type when compiling outside ASP.NET templates.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;

namespace SwarmUi.Client.Webhooks;

/// <summary>
/// Optional: helper to receive SwarmUI webhook POSTs in an ASP.NET Core app.
/// </summary>
public static class SwarmWebhookReceiverExtensions
{
    /// <summary>
    /// Maps webhook endpoints like:
    /// POST /swarm/webhook/{eventType}
    /// where {eventType} can be server-start, queue-start, every-gen, etc.
    /// </summary>
    public static IEndpointRouteBuilder MapSwarmUiWebhooks(this IEndpointRouteBuilder app, string routePrefix, Func<SwarmWebhookPayload, Task> handler)
    {
        if (string.IsNullOrWhiteSpace(routePrefix)) routePrefix = "/swarm/webhook";
        if (!routePrefix.StartsWith("/")) routePrefix = "/" + routePrefix.Trim();

        app.MapPost($"{routePrefix}/{{eventType}}", async (HttpRequest req, string eventType) =>
        {
            using var doc = await JsonDocument.ParseAsync(req.Body);
            var payload = new SwarmWebhookPayload
            {
                EventType = ParseEventType(eventType),
                Raw = doc.RootElement.Clone()
            };

            await handler(payload);
            return Results.Ok(new { ok = true });
        });

        return app;
    }

    private static SwarmWebhookEventType ParseEventType(string raw)
    {
        raw = (raw ?? "").Trim().ToLowerInvariant();
        return raw switch
        {
            "server-start" => SwarmWebhookEventType.ServerStart,
            "server-shutdown" => SwarmWebhookEventType.ServerShutdown,
            "queue-start" => SwarmWebhookEventType.QueueStart,
            "queue-end" => SwarmWebhookEventType.QueueEnd,
            "every-gen" => SwarmWebhookEventType.EveryGen,
            "manual-gen" => SwarmWebhookEventType.ManualGen,
            _ => SwarmWebhookEventType.Unknown
        };
    }
}
#endif
