namespace SuperCricket.Simulation;

public static class GameSettingsReviewChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"super-cricket-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(directory, "settings.json");
        var missingPath = Path.Combine(directory, "missing.json");
        try
        {
            var defaults = GameSettingsStore.Load(missingPath);
            Require(defaults.Difficulty == CpuDifficulty.Standard && defaults.OversPerInnings == 1 &&
                !defaults.HighContrast && !defaults.LargeText,
                "a missing settings file did not return the documented defaults");

            var saved = new GameSettings
            {
                Difficulty = CpuDifficulty.Pro,
                OversPerInnings = 5,
                HighContrast = true,
                LargeText = true
            };
            GameSettingsStore.Save(settingsPath, saved);
            var loaded = GameSettingsStore.Load(settingsPath);
            Require(loaded.Difficulty == saved.Difficulty && loaded.OversPerInnings == saved.OversPerInnings &&
                loaded.HighContrast == saved.HighContrast && loaded.LargeText == saved.LargeText,
                "saved match and accessibility preferences did not round-trip");
            Require(!File.Exists(settingsPath + ".tmp"), "a successful save left its temporary file behind");

            ExpectInvalidData(() => GameSettingsStore.Save(settingsPath, new GameSettings { Version = 99 }),
                "an unsupported settings version was accepted on save");
            ExpectInvalidData(() => GameSettingsStore.Save(settingsPath, new GameSettings { Difficulty = (CpuDifficulty)99 }),
                "an unsupported CPU difficulty was accepted on save");
            ExpectInvalidData(() => GameSettingsStore.Save(settingsPath, new GameSettings { OversPerInnings = 3 }),
                "an unsupported overs length was accepted on save");
            var unchanged = GameSettingsStore.Load(settingsPath);
            Require(unchanged.Difficulty == saved.Difficulty && unchanged.OversPerInnings == saved.OversPerInnings &&
                unchanged.HighContrast == saved.HighContrast && unchanged.LargeText == saved.LargeText,
                "a rejected save changed the previously saved settings");

            File.WriteAllText(settingsPath, "{\"version\":99}");
            ExpectInvalidData(() => GameSettingsStore.Load(settingsPath),
                "an unsupported settings version was accepted on load");
            File.WriteAllText(settingsPath, "{\"version\":1,\"difficulty\":\"impossible\"}");
            ExpectInvalidData(() => GameSettingsStore.Load(settingsPath),
                "an unsupported CPU difficulty was accepted on load");

            File.WriteAllText(settingsPath, "{ invalid json");
            ExpectInvalidData(() => GameSettingsStore.Load(settingsPath),
                "malformed settings JSON was accepted on load");

            Console.WriteLine("PASS: settings round-trip atomically, defaults load, rejected writes preserve prior data, and invalid data is rejected.");
        }
        finally
        {
            if (File.Exists(settingsPath)) File.Delete(settingsPath);
            if (File.Exists(settingsPath + ".tmp")) File.Delete(settingsPath + ".tmp");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void ExpectInvalidData(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException($"Settings check failed: {message}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Settings check failed: {message}");
    }
}
