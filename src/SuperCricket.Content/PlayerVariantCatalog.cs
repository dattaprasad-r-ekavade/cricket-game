using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

/// <summary>Validated manifest for the rig-compatible player physique and LOD exports.</summary>
public sealed class PlayerVariantCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("lod_switch_meters")]
    public float[] LodSwitchMeters { get; set; } = [];
    [JsonPropertyName("team_color_masks")]
    public PlayerVariantTeamColorMasks TeamColorMasks { get; set; } = new();
    public List<PlayerVariantDefinition> Variants { get; set; } = [];

    public static PlayerVariantCatalog Load(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
            ?? throw new InvalidDataException($"Player variant manifest '{manifestPath}' has no parent directory.");
        var catalog = JsonSerializer.Deserialize<PlayerVariantCatalog>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException($"Player variant manifest '{manifestPath}' was empty.");
        var errors = catalog.Validate(root);
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid player variant manifest '{manifestPath}': {string.Join(" ", errors)}");
        return catalog;
    }

    public List<string> Validate(string assetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetDirectory);
        var errors = new List<string>();
        if (Schema != "super-cricket.player-variants.v1")
            errors.Add("Player variant schema must be super-cricket.player-variants.v1.");
        if (LodSwitchMeters is not { Length: 2 } ||
            LodSwitchMeters.Any(value => !float.IsFinite(value) || value <= 0f) ||
            LodSwitchMeters[0] >= LodSwitchMeters[1])
            errors.Add("Player LOD switch distances must contain two increasing positive metre values.");
        if (Variants is null || Variants.Count != 3)
        {
            errors.Add("Player variant catalog must contain agile, balanced, and power variants.");
            return errors;
        }

        var root = Path.GetFullPath(assetDirectory);
        var variantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in Variants)
        {
            if (variant is null || string.IsNullOrWhiteSpace(variant.Id) || !variantIds.Add(variant.Id))
            {
                errors.Add("Player variant IDs must be unique and non-empty.");
                continue;
            }
            if (variant.Lods is not { Count: 3 } || !variant.Lods.Select(lod => lod.Lod).Order().SequenceEqual([0, 1, 2]))
            {
                errors.Add($"Player variant '{variant.Id}' must define LOD0, LOD1, and LOD2 exactly once.");
                continue;
            }

            foreach (var lod in variant.Lods)
            {
                if (string.IsNullOrWhiteSpace(lod.Glb) || Path.IsPathFullyQualified(lod.Glb))
                {
                    errors.Add($"Player variant '{variant.Id}' LOD{lod.Lod} must use a relative GLB path.");
                    continue;
                }

                var fullPath = ResolveRelativePath(root, lod.Glb);
                if (fullPath is null || !File.Exists(fullPath))
                    errors.Add($"Player variant '{variant.Id}' LOD{lod.Lod} GLB '{lod.Glb}' must exist inside the asset directory.");
            }
        }

        foreach (var id in new[] { "agile", "balanced", "power" })
        {
            if (!variantIds.Contains(id))
                errors.Add($"Player variant '{id}' is required.");
        }

        if (TeamColorMasks is null || TeamColorMasks.Jersey is null || TeamColorMasks.Trouser is null)
        {
            errors.Add("Player variant catalog must define jersey and trouser team-color masks.");
        }
        else
        {
            ValidateRelativeAssetPath(root, TeamColorMasks.Jersey.Mask, "jersey team-color mask", errors);
            ValidateRelativeAssetPath(root, TeamColorMasks.Jersey.BaseColor, "jersey base-color texture", errors);
            ValidateRelativeAssetPath(root, TeamColorMasks.Trouser.Mask, "trouser team-color mask", errors);
            ValidateRelativeAssetPath(root, TeamColorMasks.Trouser.BaseColor, "trouser base-color texture", errors);
        }

        return errors;
    }

    public int SelectLod(float cameraDistanceMeters)
    {
        if (!float.IsFinite(cameraDistanceMeters) || cameraDistanceMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(cameraDistanceMeters), "Camera distance must be finite and non-negative.");
        return cameraDistanceMeters < LodSwitchMeters[0]
            ? 0
            : cameraDistanceMeters < LodSwitchMeters[1] ? 1 : 2;
    }

    public string GetGlbPath(string assetDirectory, string variantId, int lod)
    {
        var variant = Variants.SingleOrDefault(item => string.Equals(item.Id, variantId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown player variant '{variantId}'.");
        var selectedLod = variant.Lods.SingleOrDefault(item => item.Lod == lod)
            ?? throw new KeyNotFoundException($"Player variant '{variantId}' has no LOD{lod}.");
        return Path.GetFullPath(Path.Combine(assetDirectory, selectedLod.Glb));
    }

    public void ApplyTeamColorMasks(PlayerAsset asset, string assetDirectory)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var root = Path.GetFullPath(assetDirectory);
        var jerseyMask = ResolveRelativePath(root, TeamColorMasks.Jersey.Mask)
            ?? throw new InvalidDataException("Jersey team-color mask must be inside the player variant asset directory.");
        var trouserMask = ResolveRelativePath(root, TeamColorMasks.Trouser.Mask)
            ?? throw new InvalidDataException("Trouser team-color mask must be inside the player variant asset directory.");
        asset.Textures =
        [
            MakeMaskTexture(jerseyMask),
            MakeMaskTexture(trouserMask)
        ];
        foreach (var mesh in asset.Meshes)
        {
            if (mesh.Name.StartsWith("WP19 Jersey ", StringComparison.OrdinalIgnoreCase))
            {
                mesh.BaseColorTextureIndex = 0;
                mesh.DiffuseColor = White();
            }
            else if (mesh.Name.StartsWith("WP19 Trouser ", StringComparison.OrdinalIgnoreCase))
            {
                mesh.BaseColorTextureIndex = 1;
                mesh.DiffuseColor = White();
            }
        }
        var errors = asset.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Applying player team-color masks made '{asset.Name}' invalid: {string.Join(" ", errors)}");
    }

    private static PlayerTextureData MakeMaskTexture(string path) => new()
    {
        MimeType = "image/png",
        Content = File.ReadAllBytes(path),
        MinFilter = 9729,
        MagFilter = 9729,
        WrapU = 10497,
        WrapV = 10497
    };

    private static Vector3Data White() => new() { X = 1f, Y = 1f, Z = 1f };

    private static bool ValidateRelativeAssetPath(string root, string? relativePath, string label, List<string> errors)
    {
        var fullPath = ResolveRelativePath(root, relativePath);
        if (fullPath is not null && File.Exists(fullPath))
            return true;
        errors.Add($"The {label} must exist inside the player variant asset directory.");
        return false;
    }

    private static string? ResolveRelativePath(string root, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
            return null;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var relative = Path.GetRelativePath(root, fullPath);
        return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? null
            : fullPath;
    }
}

public sealed class PlayerVariantTeamColorMasks
{
    public PlayerVariantMask Jersey { get; set; } = new();
    public PlayerVariantMask Trouser { get; set; } = new();
}

public sealed class PlayerVariantMask
{
    public string Mask { get; set; } = string.Empty;
    [JsonPropertyName("base_color")]
    public string BaseColor { get; set; } = string.Empty;
}

public sealed class PlayerVariantDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<PlayerVariantLod> Lods { get; set; } = [];
}

public sealed class PlayerVariantLod
{
    public int Lod { get; set; }
    public string Glb { get; set; } = string.Empty;
}
