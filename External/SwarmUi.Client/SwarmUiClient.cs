using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SwarmUi.Client;

/// <summary>
/// A practical SwarmUI client for prompt generators.
/// </summary>
public sealed class SwarmUiClient : ISwarmUiClient
{
    private readonly SwarmUiClientOptions _options;
    private readonly SwarmUiHttp _http;

    public string? SessionId { get; private set; }

    public SwarmUiClient(SwarmUiClientOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = new SwarmUiHttp(httpClient ?? new HttpClient(), options);
    }

    public async Task<string> EnsureSessionAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(SessionId))
            return SessionId!;

        if (!_options.AutoSession)
            throw new InvalidOperationException("AutoSession is disabled and no SessionId is set.");

        var resp = await _http.PostJsonAsync<Dictionary<string, object?>>("/API/GetNewSession", new { }, ct).ConfigureAwait(false);

        SessionId = ExtractString(resp, "session_id")
            ?? ExtractString(resp, "sessionId")
            ?? ExtractString(resp, "id");

        if (string.IsNullOrWhiteSpace(SessionId))
            throw new SwarmUiException(200, "GetNewSession succeeded but no session_id field was found in the response.");

        return SessionId!;
    }

    public async Task<TResponse> CallRouteAsync<TRequest, TResponse>(string routeName, TRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(routeName))
            throw new ArgumentException("Route name cannot be empty.", nameof(routeName));

        // Per docs: if error_id == invalid_session_id, call GetNewSession and retry.
        // We'll do one automatic retry.
        return await CallRouteWithRetryAsync<TRequest, TResponse>(routeName, request, ct).ConfigureAwait(false);
    }

    private async Task<TResponse> CallRouteWithRetryAsync<TRequest, TResponse>(string routeName, TRequest request, CancellationToken ct)
    {
        var sid = await EnsureSessionAsync(ct).ConfigureAwait(false);
        var body = MergeWithSessionId(request, sid);

        try
        {
            return await _http.PostJsonAsync<TResponse>($"/API/{routeName}", body, ct).ConfigureAwait(false);
        }
        catch (SwarmUiApiErrorException ex) when (string.Equals(ex.ErrorId, "invalid_session_id", StringComparison.OrdinalIgnoreCase))
        {
            SessionId = null;
            sid = await EnsureSessionAsync(ct).ConfigureAwait(false);
            body = MergeWithSessionId(request, sid);

            return await _http.PostJsonAsync<TResponse>($"/API/{routeName}", body, ct).ConfigureAwait(false);
        }
    }

    public async Task<SwarmStatus> GetCurrentStatusAsync(CancellationToken ct = default)
    {
        var dict = await CallRouteAsync<object, Dictionary<string, object?>>("GetCurrentStatus", new { }, ct).ConfigureAwait(false);
        return new SwarmStatus
        {
            Status = ExtractString(dict, "status"),
            Detail = ExtractString(dict, "detail"),
            Extra = dict
        };
    }

    public async Task<TestPromptFillResponse> TestPromptFillAsync(TestPromptFillRequest request, CancellationToken ct = default)
    {
        // Docs: parameters include only "prompt" (required).
        var body = new Dictionary<string, object?>
        {
            ["prompt"] = request.Prompt
        };

        foreach (var kv in request.Extra)
            body[kv.Key] = kv.Value;

        var resp = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>(
            "TestPromptFill",
            body,
            ct
        ).ConfigureAwait(false);

        return new TestPromptFillResponse
        {
            FilledPrompt = ExtractString(resp, "result") ?? ExtractString(resp, "prompt") ?? "",
            Extra = resp
        };
    }

    public async Task<GenerateText2ImageResponse> GenerateText2ImageAsync(GenerateText2ImageRequest request, CancellationToken ct = default)
    {
        // Documented minimal params are "prompt" and usually width/height; many more are supported by Swarm builds.
        var resp = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>(
            "GenerateText2Image",
            ToSwarmGenerateBody(request),
            ct
        ).ConfigureAwait(false);

        return new GenerateText2ImageResponse
        {
            Image = ExtractString(resp, "image"),
            Images = ExtractStringList(resp, "images"),
            Seed = ExtractLong(resp, "seed"),
            Prompt = ExtractString(resp, "prompt"),
            NegativePrompt = ExtractString(resp, "negativeprompt"),
            Extra = resp
        };
    }


    public async Task<bool> InterruptAllAsync(bool otherSessions = false, CancellationToken ct = default)
    {
        var resp = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>(
            "InterruptAll",
            new Dictionary<string, object?> { ["other_sessions"] = otherSessions },
            ct
        ).ConfigureAwait(false);

        // SwarmUI docs: { "success": true }
        if (resp.TryGetValue("success", out var v) && v is not null)
        {
            if (v is bool b) return b;
            if (v is JsonElement je && je.ValueKind == JsonValueKind.True) return true;
            if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
        }

        return true;
    }

    /// <summary>
    /// Generate using SwarmUI's current UI-selected settings (model, resolution, sampler, steps, etc.).
    /// This intentionally sends only the prompt (and optional negative/seed/extra) so SwarmUI remains authoritative.
    /// </summary>
    public async Task<GenerateText2ImageResponse> GenerateText2ImageUsingUiDefaultsAsync(
        string prompt,
        string? negativePrompt = null,
        long? seed = null,
        Dictionary<string, object?>? extra = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        var body = new Dictionary<string, object?>
        {
            ["prompt"] = prompt
        };

        if (!string.IsNullOrWhiteSpace(negativePrompt))
            body["negativeprompt"] = negativePrompt;

        if (seed is not null)
            body["seed"] = seed.Value;

        if (extra is not null)
        {
            foreach (var kv in extra)
                body[kv.Key] = kv.Value;
        }

        // SwarmUI currently requires a model selection for GenerateText2Image.
        // The intent of this helper is "use whatever the UI is using" — so we try to
        // read the server's current model and resolution and send those.
        //
        // Priority order:
        //  1) If caller provided model/width/height explicitly (via extra), keep them.
        //  2) Try current loaded model from server.
        //  3) Fallback to first loaded model list.
        //  4) As a last resort, omit (and Swarm will error if it truly can't infer).
        if (!body.ContainsKey("model"))
        {
            var model = await TryGetCurrentModelAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(model))
                body["model"] = model;

            // Resolution is often required as well. If we can infer it, include it.
            if (!body.ContainsKey("width") || !body.ContainsKey("height"))
            {
                var (w, h) = await TryGetCurrentResolutionAsync(model, ct).ConfigureAwait(false);
                if (!body.ContainsKey("width") && w is not null) body["width"] = w.Value;
                if (!body.ContainsKey("height") && h is not null) body["height"] = h.Value;
            }
        }

        var resp = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>(
            "GenerateText2Image",
            body,
            ct
        ).ConfigureAwait(false);

        return new GenerateText2ImageResponse
        {
            Image = ExtractString(resp, "image"),
            Images = ExtractStringList(resp, "images"),
            Seed = ExtractLong(resp, "seed"),
            Prompt = ExtractString(resp, "prompt"),
            NegativePrompt = ExtractString(resp, "negativeprompt"),
            Extra = resp
        };
    }

    private async Task<string?> TryGetCurrentModelAsync(CancellationToken ct)
    {
        // Some Swarm builds expose the currently loaded model in GetCurrentStatus.
        try
        {
            var status = await CallRouteAsync<object, Dictionary<string, object?>>("GetCurrentStatus", new { }, ct).ConfigureAwait(false);
            var fromStatus = ExtractString(status, "model")
                ?? ExtractString(status, "current_model")
                ?? ExtractString(status, "currentModel");
            if (!string.IsNullOrWhiteSpace(fromStatus))
                return fromStatus;
        }
        catch
        {
            // best effort
        }

        // Next best: list loaded models, pick the first.
        try
        {
            var dict = await CallRouteAsync<object, Dictionary<string, object?>>("ListLoadedModels", new { }, ct).ConfigureAwait(false);
            if (dict.TryGetValue("models", out var modelsObj) && modelsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            return name;
                    }
                }
            }
        }
        catch
        {
            // best effort
        }

        return null;
    }

    /// <summary>
    /// Lists available Stable-Diffusion models on the SwarmUI server (best effort).
    /// This is used by PromptLoom's SwarmUI settings tab to populate a model dropdown.
    /// </summary>
    public async Task<List<string>> ListStableDiffusionModelsAsync(int depth = 6, CancellationToken ct = default)
    {
        try
        {
            var dict = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>("ListModels",
                new Dictionary<string, object?>
                {
                    ["path"] = "",
                    ["depth"] = depth,
                    ["subtype"] = "Stable-Diffusion",
                    ["sortBy"] = "Name",
                    ["sortReverse"] = false,
                    ["allowRemote"] = true,
                    ["dataImages"] = false
                }, ct).ConfigureAwait(false);

            var results = new List<string>();

            if (dict.TryGetValue("files", out var filesObj) && filesObj is JsonElement files && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in files.EnumerateArray())
                {
                    if (file.ValueKind == JsonValueKind.Object && file.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            results.Add(name);
                    }
                }
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Lists available LoRA files on the SwarmUI server (best effort).
    /// </summary>
    public async Task<List<string>> ListLorasAsync(int depth = 6, CancellationToken ct = default)
    {
        try
        {
            var dict = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>("ListModels",
                new Dictionary<string, object?>
                {
                    ["path"] = "",
                    ["depth"] = depth,
                    ["subtype"] = "LoRA",
                    ["sortBy"] = "Name",
                    ["sortReverse"] = false,
                    ["allowRemote"] = true,
                    ["dataImages"] = false
                }, ct).ConfigureAwait(false);

            var results = new List<string>();

            if (dict.TryGetValue("files", out var filesObj) && filesObj is JsonElement files && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in files.EnumerateArray())
                {
                    if (file.ValueKind == JsonValueKind.Object && file.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            results.Add(name);
                    }
                }
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }
        catch
        {
            return new List<string>();
        }
    }


    /// <summary>
    /// Best-effort helper to infer the model and a safe resolution that SwarmUI will accept.
    /// This mirrors the logic used by GenerateText2ImageUsingUiDefaultsAsync, but is exposed
    /// so callers can use the WebSocket streaming endpoint too.
    /// </summary>
    public async Task<(string? model, int width, int height)> GetSuggestedModelAndResolutionAsync(CancellationToken ct = default)
    {
        // FIX: SwarmUI API error "No model input given. Did your UI load properly?"
        // CAUSE: SwarmUI's API does not implicitly inherit the Web UI's selected model.
        // CHANGE: Always return a non-null model fallback when we can't infer a loaded model.
        // DATE: 2025-12-22
        var model = await TryGetCurrentModelAsync(ct).ConfigureAwait(false);
        model ??= "OfficialStableDiffusion/sd_xl_base_1.0";
        var (w, h) = await TryGetCurrentResolutionAsync(model, ct).ConfigureAwait(false);
        return (model, w ?? 1024, h ?? 1024);
    }

    private async Task<(int? width, int? height)> TryGetCurrentResolutionAsync(string? modelName, CancellationToken ct)
    {
        // If we can describe the model, use its standard width/height as a safe default.
        // This isn't always the same as the UI's currently selected resolution, but it's
        // better than sending nothing when the API requires dimensions.
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            try
            {
                var dict = await CallRouteAsync<Dictionary<string, object?>, Dictionary<string, object?>>("DescribeModel",
                    new Dictionary<string, object?> { ["modelName"] = modelName, ["subtype"] = "Stable-Diffusion" }, ct).ConfigureAwait(false);

                if (dict.TryGetValue("model", out var modelObj) && modelObj is JsonElement je && je.ValueKind == JsonValueKind.Object)
                {
                    int? w = null;
                    int? h = null;
                    if (je.TryGetProperty("standard_width", out var wEl) && wEl.TryGetInt32(out var wi)) w = wi;
                    if (je.TryGetProperty("standard_height", out var hEl) && hEl.TryGetInt32(out var hi)) h = hi;
                    if (w is not null && h is not null)
                        return (w, h);
                }
            }
            catch
            {
                // best effort
            }
        }

        return (1024, 1024);
    }

    public async IAsyncEnumerable<string> GenerateText2ImageStreamAsync(GenerateText2ImageRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Many Swarm builds expose a WS endpoint at /API/GenerateText2ImageWS.
        // This yields raw JSON text frames. If your Swarm build differs, adjust endpoint/protocol accordingly.
        var sid = await EnsureSessionAsync(ct).ConfigureAwait(false);
        var body = ToSwarmGenerateBody(request);
        body["session_id"] = sid;

        var wsUri = ToWebSocketUri(_options.BaseUrl, "/API/GenerateText2ImageWS");

        using var ws = new ClientWebSocket();

        if (!string.IsNullOrWhiteSpace(_options.SwarmToken))
            ws.Options.SetRequestHeader("Cookie", $"swarm_token={_options.SwarmToken}");

        await ws.ConnectAsync(wsUri, ct).ConfigureAwait(false);

        var sendJson = JsonSerializer.Serialize(body, SwarmJson.Options);
        var sendBytes = Encoding.UTF8.GetBytes(sendJson);
        await ws.SendAsync(sendBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct).ConfigureAwait(false);

        var buffer = new byte[64 * 1024];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult? result;

            do
            {
                result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", ct).ConfigureAwait(false);
                    yield break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            var msg = sb.ToString();
            if (!string.IsNullOrWhiteSpace(msg))
                yield return msg;
        }
    }

    private static Uri ToWebSocketUri(Uri baseUrl, string path)
    {
        var builder = new UriBuilder(baseUrl)
        {
            Scheme = baseUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = path.StartsWith("/") ? path : "/" + path
        };
        return builder.Uri;
    }

    private static Dictionary<string, object?> MergeWithSessionId<TRequest>(TRequest request, string sessionId)
    {
        if (request is Dictionary<string, object?> dict)
        {
            var copy = new Dictionary<string, object?>(dict);
            copy["session_id"] = sessionId;
            return copy;
        }

        var json = JsonSerializer.Serialize(request, SwarmJson.Options);
        var obj = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, SwarmJson.Options) ?? new();
        obj["session_id"] = sessionId;
        return obj;
    }

    private static Dictionary<string, object?> ToSwarmGenerateBody(GenerateText2ImageRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["prompt"] = request.Prompt,
            ["negativeprompt"] = request.NegativePrompt,
            // SwarmUI API generally requires an explicit model; do not rely on Web UI state.
            ["model"] = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model,
            ["width"] = request.Width,
            ["height"] = request.Height
        };

        // Remove nulls to keep payload tidy.
        if (body["model"] is null)
            body.Remove("model");

        if (request.Steps is not null)
            body["steps"] = request.Steps.Value;

        if (request.CfgScale is not null)
            body["cfgscale"] = request.CfgScale.Value;

        if (!string.IsNullOrWhiteSpace(request.Sampler))
            body["sampler"] = request.Sampler;

        if (request.Seed is not null)
            body["seed"] = request.Seed.Value;

        foreach (var kv in request.Extra)
            body[kv.Key] = kv.Value;

        return body;
    }

    private static string? ExtractString(Dictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static long? ExtractLong(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return null;
        if (v is long l) return l;
        if (v is int i) return i;
        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt64(out var n)) return n;
            if (je.ValueKind == JsonValueKind.String && long.TryParse(je.GetString(), out var s)) return s;
        }
        if (long.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static List<string> ExtractStringList(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return new();
        if (v is List<string> ls) return ls;

        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var result = new List<string>();
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) result.Add(item.GetString() ?? "");
                else result.Add(item.ToString());
            }
            return result;
        }

        return new List<string> { v.ToString() ?? "" };
    }
}
