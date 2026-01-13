// CHANGE LOG
// - 2026-03-12 | Request: Restore window position | Persist window bounds and state in user settings.
// - 2026-03-02 | Request: Batch qty slider | Default batch quantity to 2.
// - 2025-12-31 | Request: Persist SwarmUI toggles | Store send-seed toggle and SwarmUI selections.
// FIX: Introduce user settings store seam to allow in-memory testing.
// CAUSE: MainViewModel read/wrote user_settings.json directly.
// CHANGE: Add IUserSettingsStore and a default implementation. 2025-12-25

using System.Text.Json;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for user settings persistence.
/// </summary>
public interface IUserSettingsStore
{
    /// <summary>
    /// Loads user settings from storage, or returns null if missing/invalid.
    /// </summary>
    UserSettings? Load();

    /// <summary>
    /// Saves user settings to storage.
    /// </summary>
    void Save(UserSettings settings);
}

/// <summary>
/// Default user settings store using JSON and the file system.
/// </summary>
public sealed class UserSettingsStore : IUserSettingsStore
{
    private readonly IFileSystem _fileSystem;
    private readonly IAppDataStore _appDataStore;

    /// <summary>
    /// Creates a new user settings store.
    /// </summary>
    public UserSettingsStore(IFileSystem fileSystem, IAppDataStore appDataStore)
    {
        _fileSystem = fileSystem;
        _appDataStore = appDataStore;
    }

    private string SettingsPath => System.IO.Path.Combine(_appDataStore.RootDir, "user_settings.json");

    /// <inheritdoc/>
    public UserSettings? Load()
    {
        if (!_fileSystem.FileExists(SettingsPath))
            return null;

        return JsonSerializer.Deserialize<UserSettings>(_fileSystem.ReadAllText(SettingsPath));
    }

    /// <inheritdoc/>
    public void Save(UserSettings settings)
    {
        _fileSystem.CreateDirectory(_appDataStore.RootDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.WriteAllText(SettingsPath, json);
    }
}

/// <summary>
/// Serializable settings payload for user configuration.
/// </summary>
public sealed class UserSettings
{
    public string SwarmUrl { get; set; } = "http://127.0.0.1:7801";
    public string SwarmToken { get; set; } = "";

    public bool SendSwarmModelOverride { get; set; } = true;
    public string? SwarmSelectedModel { get; set; }

    public bool SendSwarmSteps { get; set; }
    public int SwarmSteps { get; set; }

    public bool SendSwarmCfgScale { get; set; }
    public double SwarmCfgScale { get; set; }

    public bool SendSwarmSeed { get; set; } = true;

    public bool SendSwarmLoras { get; set; }
    public string? SwarmSelectedLora1 { get; set; }
    public double SwarmLora1Weight { get; set; } = 1.0;
    public string? SwarmSelectedLora2 { get; set; }
    public double SwarmLora2Weight { get; set; } = 1.0;

    public int BatchQty { get; set; } = 2;
    public bool BatchRandomizePrompts { get; set; }

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public string? WindowState { get; set; }
}
