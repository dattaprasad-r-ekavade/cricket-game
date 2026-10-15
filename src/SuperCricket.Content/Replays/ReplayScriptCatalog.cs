using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content.Replays;

public sealed record ReplayScriptShot(
    string CameraPreset,
    float StartOffsetSeconds,
    float DurationSeconds,
    float Speed,
    string Overlay);

public sealed record ReplayEventScript(string EventType, IReadOnlyList<ReplayScriptShot> Shots);

public sealed record ReplayScriptCatalog(IReadOnlyList<ReplayEventScript> Events)
{
    private static readonly string[] RequiredEventTypes =
        ["bowled", "caught", "lbw", "run-out", "six", "near-run-out"];

    private static readonly string[] CameraPresets =
        ["recorded", "broadcast", "behind-striker", "bowler-end", "square-leg", "umpire-close-up", "ball-follow"];

    private static readonly string[] Overlays = ["none", "stump-line-strip", "crease-zoom"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ReplayScriptCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<ReplayScriptCatalog>(json, JsonOptions)
            ?? throw new InvalidDataException("Replay script catalog is empty.");
        var errors = catalog.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return catalog;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Events is null || Events.Count == 0)
            return ["Replay script catalog must contain event scripts."];

        var duplicateTypes = Events.GroupBy(script => script.EventType, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        errors.AddRange(duplicateTypes.Select(eventType => $"Replay event '{eventType}' is declared more than once."));

        foreach (var required in RequiredEventTypes)
        {
            if (!Events.Any(script => string.Equals(script.EventType, required, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Replay event '{required}' is missing.");
        }

        foreach (var script in Events)
        {
            if (!RequiredEventTypes.Contains(script.EventType, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Replay event '{script.EventType}' is not supported.");
            if (script.Shots is null || script.Shots.Count == 0)
            {
                errors.Add($"Replay event '{script.EventType}' must contain at least one shot.");
                continue;
            }

            for (var shotIndex = 0; shotIndex < script.Shots.Count; shotIndex++)
            {
                var shot = script.Shots[shotIndex];
                var label = $"Replay event '{script.EventType}', shot {shotIndex + 1}";
                if (!CameraPresets.Contains(shot.CameraPreset, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"{label} names unknown camera preset '{shot.CameraPreset}'.");
                if (!float.IsFinite(shot.StartOffsetSeconds) || shot.StartOffsetSeconds > 0f)
                    errors.Add($"{label} start offset must be finite and at or before the event.");
                if (!float.IsFinite(shot.DurationSeconds) || shot.DurationSeconds <= 0f)
                    errors.Add($"{label} duration must be positive and finite.");
                if (shot.Speed is not (1f or 0.5f or 0.25f))
                    errors.Add($"{label} speed must be 1, 0.5, or 0.25.");
                if (!Overlays.Contains(shot.Overlay, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"{label} names unknown overlay '{shot.Overlay}'.");
                if (string.Equals(shot.Overlay, "stump-line-strip", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(script.EventType, "lbw", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{label} uses the LBW stump-line overlay for a non-LBW event.");
                if (string.Equals(shot.Overlay, "crease-zoom", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(script.EventType, "run-out", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(script.EventType, "near-run-out", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{label} uses the crease overlay for a non-run-out event.");
            }
        }

        return errors;
    }

    public ReplayEventScript GetEvent(string eventType) =>
        Events.First(script => string.Equals(script.EventType, eventType, StringComparison.OrdinalIgnoreCase));
}
