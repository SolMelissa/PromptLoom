using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SwarmUi.Client;

internal sealed class SwarmUiHttp
{
    private readonly HttpClient _http;
    private readonly SwarmUiClientOptions _options;

    public SwarmUiHttp(HttpClient http, SwarmUiClientOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _http.BaseAddress = options.BaseUrl;
        _http.Timeout = options.Timeout;

        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

        if (!string.IsNullOrWhiteSpace(options.SwarmToken))
        {
            // SwarmUI auth uses a cookie called "swarm_token".
            // Using a simple Cookie header works well for single-cookie auth flows.
            _http.DefaultRequestHeaders.Remove("Cookie");
            _http.DefaultRequestHeaders.Add("Cookie", $"swarm_token={options.SwarmToken}");
        }

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<TResponse> PostJsonAsync<TResponse>(string path, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, SwarmJson.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(path, content, ct).ConfigureAwait(false);
        var respText = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new SwarmUiException((int)resp.StatusCode, $"SwarmUI returned {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(respText, 800)}");

        // SwarmUI may return 200 with { error: "...", error_id: "..." }.
        // Detect that early and throw a typed exception.
        TryThrowSwarmError(respText);

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(respText, SwarmJson.Options);
            if (result is null)
                throw new SwarmUiException((int)resp.StatusCode, "SwarmUI returned empty JSON response.");
            return result;
        }
        catch (JsonException jx)
        {
            throw new SwarmUiException((int)resp.StatusCode, $"Failed to parse SwarmUI JSON: {jx.Message}. Body: {Truncate(respText, 800)}", jx);
        }
    }

    private static void TryThrowSwarmError(string respText)
    {
        try
        {
            using var doc = JsonDocument.Parse(respText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            string? error = null;
            string? errorId = null;

            if (doc.RootElement.TryGetProperty("error", out var e))
                error = e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();

            if (doc.RootElement.TryGetProperty("error_id", out var ei))
                errorId = ei.ValueKind == JsonValueKind.String ? ei.GetString() : ei.ToString();

            if (!string.IsNullOrWhiteSpace(error) || !string.IsNullOrWhiteSpace(errorId))
            {
                var msg = !string.IsNullOrWhiteSpace(error)
                    ? error!
                    : $"SwarmUI error_id: {errorId}";

                throw new SwarmUiApiErrorException(errorId, msg);
            }
        }
        catch (JsonException)
        {
            // Not JSON, ignore.
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
