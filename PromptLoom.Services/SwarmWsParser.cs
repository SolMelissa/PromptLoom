// CHANGE LOG
// - 2025-12-31 | Request: Show step progress | Extract step/total step counts from SwarmUI frames.
// - 2025-12-22 | Fix: Parse SwarmUI progress frames | Extract status/progress/preview/final image refs.
// - 2025-12-22 | Fix: Tolerant websocket parsing | Support varied SwarmUI JSON schemas.
/*
FIX: Parse SwarmUI GenerateText2ImageWS progress frames for UI updates.
CAUSE: SwarmUI websocket message shapes vary across versions; hardcoding a single schema breaks easily.
CHANGE: Implemented tolerant JSON parsing to extract status/progress/preview/final image refs when present. 2025-12-22
*/

using System.Text.Json;

namespace PromptLoom.Services;

public static class SwarmWsParser
{
    public static bool TryParseSwarmWsFrame(
        string json,
        out string? status,
        out double? progress01,
        out int? step,
        out int? steps,
        out string? previewDataUrl,
        out string? finalImageRef,
        out long? seed)
    {
        status = null;
        progress01 = null;
        step = null;
        steps = null;
        previewDataUrl = null;
        finalImageRef = null;
        seed = null;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Status/message
            status =
                TryGetString(root, "status") ??
                TryGetString(root, "message") ??
                TryGetString(root, "detail") ??
                TryGetString(root, "stage");

            // Progress: try percent/progress or derive from step counts.
            TryGetStepData(root, out step, out steps);
            progress01 =
                TryGetProgress(root) ??
                TryGetProgressFromSteps(step, steps);

            // Preview image: usually data url or base64
            previewDataUrl =
                TryGetString(root, "preview") ??
                TryGetString(root, "preview_image") ??
                TryGetString(root, "image_preview") ??
                TryGetString(root, "live_preview") ??
                null;

            // Final image: could be image or images[0] or result fields
            var img = TryGetString(root, "image");
            if (!string.IsNullOrWhiteSpace(img) && !img.StartsWith("data:image"))
                finalImageRef = img;

            if (finalImageRef is null)
            {
                if (root.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in imgs.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                        {
                            finalImageRef = el.GetString();
                            break;
                        }
                    }
                }
            }

            // If the "image" is a data url, treat it as preview.
            if (!string.IsNullOrWhiteSpace(img) && img.StartsWith("data:image"))
                previewDataUrl ??= img;

            // Seed: often included in the final frame, but some builds may include it earlier.
            seed = TryGetLong(root, "seed") ?? TryGetLong(root, "seed_used") ?? TryGetLong(root, "used_seed");

            return status is not null || progress01 is not null || previewDataUrl is not null || finalImageRef is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;

        if (el.ValueKind == JsonValueKind.String) return el.GetString();
        if (el.ValueKind == JsonValueKind.Number) return el.ToString();
        if (el.ValueKind == JsonValueKind.Object || el.ValueKind == JsonValueKind.Array) return el.ToString();
        return null;
    }

    private static double? TryGetProgress(JsonElement root)
    {
        // Try "progress" (0..1), "percent" (0..100), "progress_percent"
        foreach (var key in new[] { "progress", "percent", "progress_percent", "pct" })
        {
            if (!root.TryGetProperty(key, out var el))
                continue;

            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
            {
                if (d > 1.5) return d / 100.0;
                return d;
            }
            if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var ds))
            {
                if (ds > 1.5) return ds / 100.0;
                return ds;
            }
        }

        return null;
    }

    private static void TryGetStepData(JsonElement root, out int? step, out int? steps)
    {
        // step / steps or current_step / total_steps
        step = TryGetInt(root, "step") ?? TryGetInt(root, "current_step") ?? TryGetInt(root, "cur_step");
        steps = TryGetInt(root, "steps") ?? TryGetInt(root, "total_steps") ?? TryGetInt(root, "max_steps");
    }

    private static double? TryGetProgressFromSteps(int? step, int? steps)
    {
        if (step is not null && steps is not null && steps.Value > 0)
            return (double)step.Value / steps.Value;

        return null;
    }

    private static int? TryGetInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
        return null;
    }

    private static long? TryGetLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var l)) return l;
        if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var s)) return s;
        return null;
    }
}
