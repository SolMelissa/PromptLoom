using System.Text.Json;

namespace SwarmUi.Client.Webhooks;

/// <summary>
/// A flexible webhook payload container.
/// SwarmUI's exact field names depend on the hook config and template.
/// </summary>
public sealed class SwarmWebhookPayload
{
    public SwarmWebhookEventType EventType { get; init; } = SwarmWebhookEventType.Unknown;

    /// <summary>Raw JSON payload as received.</summary>
    public JsonElement Raw { get; init; }

    /// <summary>Convenience accessor for common fields.</summary>
    public string? Prompt => TryGetString("prompt");
    public string? NegativePrompt => TryGetString("negativeprompt");
    public string? Image => TryGetString("image");

    public string? TryGetString(string key)
    {
        if (Raw.ValueKind != JsonValueKind.Object) return null;
        if (!Raw.TryGetProperty(key, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }
}
