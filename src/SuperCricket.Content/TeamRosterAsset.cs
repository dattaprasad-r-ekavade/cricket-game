using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuperCricket.Content.Bowling;

namespace SuperCricket.Content;

/// <summary>A validated fictional team and its ordered batting roster.</summary>
public sealed class TeamRosterAsset
{
    public const int RosterSize = 11;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly HashSet<string> SupportedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Batter", "AllRounder", "Bowler", "Wicketkeeper"
    };

    private static readonly HashSet<string> SupportedBodyVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        "agile", "balanced", "power"
    };

    public int Version { get; set; } = 2;
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
        if (roster.Version == 1)
            roster.MigrateLegacyRatings();
        var errors = roster.Validate().Where(message => !IsWarning(message)).ToArray();
        if (errors.Length > 0)
            throw new InvalidDataException($"Invalid team roster '{path}': {string.Join(" ", errors)}");
        return roster;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 2) errors.Add($"Unsupported team roster version {Version}; expected 2.");
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
        var bowlingOptions = 0;
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
            if (!SupportedBodyVariants.Contains(player.BodyVariant ?? string.Empty))
                errors.Add($"Roster player '{player.Name}' body variant must be agile, balanced, or power.");
            if (string.Equals(player.Role, "Wicketkeeper", StringComparison.OrdinalIgnoreCase))
                wicketkeepers++;
            if (string.Equals(player.Role, "Bowler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(player.Role, "AllRounder", StringComparison.OrdinalIgnoreCase))
                bowlingOptions++;
            if (player.Timing is < 0 or > 100)
                errors.Add($"Roster player '{player.Name}' timing must be between 0 and 100.");
            if (player.Power is < 0 or > 100)
                errors.Add($"Roster player '{player.Name}' power must be between 0 and 100.");
            if (player.Bowling is < 0 or > 100)
                errors.Add($"Roster player '{player.Name}' bowling skill must be between 0 and 100.");
            if (player.Fielding is < 0 or > 100)
                errors.Add($"Roster player '{player.Name}' fielding skill must be between 0 and 100.");

            ValidateRatingBand(errors, player.Name, "batting", player.BattingRatings?.Values());
            ValidateRatingBand(errors, player.Name, "bowling", player.BowlingAttributes?.Values());
            ValidateRatingBand(errors, player.Name, "fielding", player.FieldingAttributes?.Values());
            if (player.BattingRatings is null || player.BowlingAttributes is null || player.FieldingAttributes is null)
                errors.Add($"Roster player '{player.Name}' must define batting, bowling, and fielding ratings.");
            else
            {
                var batting = player.BattingRatings.Values().ToArray();
                if (batting.Max() - batting.Min() >= 20)
                    errors.Add($"Warning: {player.Name}'s batting ratings span {batting.Max() - batting.Min()} points; keep the spread under 20 except for one or two signature strengths.");

                var fielding = player.FieldingAttributes.Values().ToArray();
                var isFieldingSpecialist = string.Equals(player.Role, "Wicketkeeper", StringComparison.OrdinalIgnoreCase);
                if (fielding.Any(value => value < 60 || value > 90) &&
                    !(isFieldingSpecialist && fielding.All(value => value is >= 40 and <= 98)))
                    errors.Add($"Warning: {player.Name}'s fielding ratings should be 60–90 except for a specialist.");
            }
            if (!Enum.IsDefined(player.BattingStyle))
                errors.Add($"Warning: {player.Name} has an unknown batting style.");
            if (!Enum.IsDefined(player.Handedness))
                errors.Add($"Warning: {player.Name} has unknown handedness.");
            if (player.BowlingAttributes is not null && !Enum.IsDefined(player.BowlingAttributes.Style))
                errors.Add($"Warning: {player.Name} has an unknown bowling style.");
        }

        if (wicketkeepers != 1)
            errors.Add("A team roster must have exactly one wicketkeeper.");
        if (bowlingOptions == 0)
            errors.Add("A team roster must have at least one bowler or all-rounder.");
        return errors;
    }

    public static bool IsWarning(string message) => message.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase);

    private void MigrateLegacyRatings()
    {
        foreach (var player in Players ?? [])
        {
            player.BattingRatings = new BattingRatingsData
            {
                FrontFoot = ToRating(player.Timing),
                BackFoot = ToRating(player.Timing - 2),
                OffSide = ToRating(player.Timing + 2),
                LegSide = ToRating(player.Timing),
                Lofted = ToRating((player.Timing + player.Power) / 2),
                Concentration = ToRating(player.Timing)
            };
            player.BowlingAttributes = new BowlingRatingsData
            {
                Style = BowlerStyle.Pace,
                Accuracy = ToRating(player.Bowling),
                Pace = ToRating(player.Bowling),
                Movement = ToRating((player.Bowling + 50) / 2),
                Variation = ToRating((player.Bowling + 40) / 2),
                Stamina = 65
            };
            var fielding = ToRating(player.Fielding);
            player.FieldingAttributes = new FieldingRatingsData
            {
                Catching = string.Equals(player.Role, "Wicketkeeper", StringComparison.OrdinalIgnoreCase)
                    ? Math.Clamp(fielding, 90, 98) : Math.Clamp(fielding, 60, 90),
                Throwing = Math.Clamp(fielding, 60, 90),
                Accuracy = Math.Clamp(fielding, 60, 90),
                Speed = Math.Clamp(fielding, 60, 90)
            };
            player.BattingStyle = player.Power >= player.Timing + 10
                ? BatterStyle.Aggressor
                : player.Timing >= player.Power + 10 ? BatterStyle.Anchor : BatterStyle.Balanced;
            player.Handedness = player.BattingOrder % 3 == 0 ? BatterHandedness.Left : BatterHandedness.Right;
        }
        Version = 2;
    }

    private static int ToRating(int value) => Math.Clamp(value, 40, 98);

    private static void ValidateRatingBand(List<string> messages, string playerName, string group, IEnumerable<int>? ratings)
    {
        if (ratings is null)
        {
            messages.Add($"Roster player '{playerName}' is missing {group} ratings.");
            return;
        }
        foreach (var rating in ratings)
        {
            if (rating is < 40 or > 98)
                messages.Add($"Warning: {playerName}'s {group} rating {rating} is outside the recommended 40–98 band.");
        }
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
            Version = 2,
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
                Power = 50,
                Bowling = order <= 4 ? 35 : order <= 7 ? 65 : 82,
                Fielding = order == 1 ? 90 : order <= 4 ? 60 : order <= 7 ? 68 : 55
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
    public string BodyVariant { get; set; } = "balanced";
    public int Timing { get; set; } = 50;
    public int Power { get; set; } = 50;
    public int Bowling { get; set; } = 50;
    public int Fielding { get; set; } = 50;
    public BattingRatingsData BattingRatings { get; set; } = new();
    public BowlingRatingsData BowlingAttributes { get; set; } = new();
    public FieldingRatingsData FieldingAttributes { get; set; } = new();
    public BatterStyle BattingStyle { get; set; } = BatterStyle.Balanced;
    public BatterHandedness Handedness { get; set; } = BatterHandedness.Right;
}

public enum BatterStyle { Anchor, Balanced, Aggressor }
public enum BatterHandedness { Right, Left }

public sealed class BattingRatingsData
{
    public int FrontFoot { get; set; } = 68;
    public int BackFoot { get; set; } = 66;
    public int OffSide { get; set; } = 68;
    public int LegSide { get; set; } = 68;
    public int Lofted { get; set; } = 68;
    public int Concentration { get; set; } = 68;
    public IEnumerable<int> Values() => [FrontFoot, BackFoot, OffSide, LegSide, Lofted, Concentration];
}

public sealed class BowlingRatingsData
{
    public BowlerStyle Style { get; set; } = BowlerStyle.Pace;
    public int Accuracy { get; set; } = 65;
    public int Pace { get; set; } = 65;
    public int Movement { get; set; } = 65;
    public int Variation { get; set; } = 65;
    public int Stamina { get; set; } = 65;
    public IEnumerable<int> Values() => [Accuracy, Pace, Movement, Variation, Stamina];
}

public sealed class FieldingRatingsData
{
    public int Catching { get; set; } = 68;
    public int Throwing { get; set; } = 68;
    public int Accuracy { get; set; } = 68;
    public int Speed { get; set; } = 68;
    public IEnumerable<int> Values() => [Catching, Throwing, Accuracy, Speed];
}
