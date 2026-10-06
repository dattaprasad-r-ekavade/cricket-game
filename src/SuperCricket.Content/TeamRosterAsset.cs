using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

/// <summary>A validated fictional team and its ordered batting roster.</summary>
public sealed class TeamRosterAsset
{
    public const int RosterSize = 11;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly HashSet<string> SupportedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Batter", "AllRounder", "Bowler", "Wicketkeeper"
    };

    public int Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string PrimaryKitColorHex { get; set; } = "#1575B8";
    public string AccentKitColorHex { get; set; } = "#F2BB46";
    public Vector3 PrimaryKitColor => ParseKitColor(PrimaryKitColorHex);
    public Vector3 AccentKitColor => ParseKitColor(AccentKitColorHex);
    public List<TeamPlayerData> Players { get; set; } = [];

    public static Vector3 ParseKitColor(string? value)
    {
        if (!IsHexColor(value))
            throw new ArgumentException("Kit color must use #RRGGBB format.", nameof(value));

        return new Vector3(
            Convert.ToByte(value!.Substring(1, 2), 16) / 255f,
            Convert.ToByte(value.Substring(3, 2), 16) / 255f,
            Convert.ToByte(value.Substring(5, 2), 16) / 255f);
    }

    public static TeamRosterAsset Load(string path)
    {
        var json = File.ReadAllText(path);
        var roster = JsonSerializer.Deserialize<TeamRosterAsset>(json, JsonOptions)
            ?? throw new InvalidDataException($"Team roster '{path}' was empty.");
        var errors = roster.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid team roster '{path}': {string.Join(" ", errors)}");
        return roster;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add($"Unsupported team roster version {Version}; expected 1.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 32)
            errors.Add("Team name must contain between 1 and 32 characters.");
        if (string.IsNullOrWhiteSpace(ShortName) || ShortName.Trim().Length > 6)
            errors.Add("Team short name must contain between 1 and 6 characters.");
        if (!IsHexColor(PrimaryKitColorHex))
            errors.Add("Primary kit color must use #RRGGBB format.");
        if (!IsHexColor(AccentKitColorHex))
            errors.Add("Accent kit color must use #RRGGBB format.");
        if (Players is null || Players.Count != RosterSize)
        {
            errors.Add($"A team roster must contain exactly {RosterSize} players.");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orders = new HashSet<int>();
        var wicketkeepers = 0;
        for (var index = 0; index < Players.Count; index++)
        {
            var player = Players[index];
            if (player is null)
            {
                errors.Add($"Roster player {index + 1} is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(player.Id) || !ids.Add(player.Id.Trim()))
                errors.Add($"Roster player {index + 1} must have a unique, non-empty id.");
            if (string.IsNullOrWhiteSpace(player.Name) || player.Name.Trim().Length > 32 || !names.Add(player.Name.Trim()))
                errors.Add($"Roster player {index + 1} must have a unique name of at most 32 characters.");
            if (player.BattingOrder is < 1 or > RosterSize || !orders.Add(player.BattingOrder))
                errors.Add($"Roster player '{player.Name}' must have a unique batting order from 1 to {RosterSize}.");
            if (!SupportedRoles.Contains(player.Role ?? string.Empty))
                errors.Add($"Roster player '{player.Name}' has an unsupported role.");
            if (string.Equals(player.Role, "Wicketkeeper", StringComparison.OrdinalIgnoreCase))
                wicketkeepers++;
            if (player.Timing is < 0 or > 100)
                errors.Add($"Roster player '{player.Name}' timing must be between 0 and 100.");
            if (player.Power is < 0 or > 100)
                errors.Add($"Roster player '{player.Name}' power must be between 0 and 100.");
        }

        if (wicketkeepers != 1)
            errors.Add("A team roster must have exactly one wicketkeeper.");
        return errors;
    }

    public TeamPlayerData GetPlayerAtBattingOrder(int battingOrder)
    {
        foreach (var player in Players)
        {
            if (player.BattingOrder == battingOrder)
                return player;
        }
        throw new InvalidOperationException($"No player has batting order {battingOrder} in team '{Name}'.");
    }

    public static TeamRosterAsset CreatePlaceholder(string name)
    {
        var safeName = string.IsNullOrWhiteSpace(name) ? "Practice XI" : name.Trim();
        var shortName = safeName.Length <= 6 ? safeName : safeName[..6];
        return new TeamRosterAsset
        {
            Name = safeName,
            ShortName = shortName,
            PrimaryKitColorHex = "#1575B8",
            AccentKitColorHex = "#F2BB46",
            Players = Enumerable.Range(1, RosterSize).Select(order => new TeamPlayerData
            {
                Id = $"player-{order:00}",
                Name = $"Player {order:00}",
                BattingOrder = order,
                Role = order == 1 ? "Wicketkeeper" : order <= 4 ? "Batter" : order <= 7 ? "AllRounder" : "Bowler",
                Timing = 50,
                Power = 50
            }).ToList()
        };
    }

    private static bool IsHexColor(string? value)
    {
        if (value is not { Length: 7 } || value[0] != '#')
            return false;
        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                return false;
        }
        return true;
    }
}

public sealed class TeamPlayerData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BattingOrder { get; set; }
    public string Role { get; set; } = string.Empty;
    public int Timing { get; set; } = 50;
    public int Power { get; set; } = 50;
}
