using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Simulation;

public sealed class GameSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public CpuDifficulty Difficulty { get; set; } = CpuDifficulty.Standard;
    public int OversPerInnings { get; set; } = 1;
    public bool HighContrast { get; set; }
    public bool LargeText { get; set; }
    public float EffectsVolume { get; set; } = 0.5f;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != CurrentVersion)
            errors.Add($"Settings version must be {CurrentVersion}.");
        if (!Enum.IsDefined(Difficulty))
            errors.Add("CPU difficulty is not supported.");
        if (OversPerInnings is not (1 or 2 or 5 or 10))
            errors.Add("Overs per innings must be 1, 2, 5, or 10.");
        if (!float.IsFinite(EffectsVolume) || EffectsVolume is < 0f or > 1f)
            errors.Add("Effects volume must be between 0 and 1.");
        return errors;
    }
}

/// <summary>Loads and atomically saves versioned user preferences without graphics dependencies.</summary>
public static class GameSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static string DefaultPath
    {
        get
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationData))
                throw new InvalidOperationException("The local application-data directory is unavailable.");
            return Path.Combine(localApplicationData, "SuperCricket", "settings.json");
        }
    }

    public static GameSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return new GameSettings();

        GameSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("The saved game settings file was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The saved game settings file contains invalid JSON or values.", exception);
        }

        var errors = settings.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid game settings: {string.Join(" ", errors)}");
        return settings;
    }

    public static void Save(string path, GameSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        var errors = settings.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Cannot save invalid game settings: {string.Join(" ", errors)}");

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Settings path must include a directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions), new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
