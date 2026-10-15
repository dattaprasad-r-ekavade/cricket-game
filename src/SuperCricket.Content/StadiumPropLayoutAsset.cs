using System.Text.Json;

namespace SuperCricket.Content;

/// <summary>Data-driven placements for static stadium and equipment props in the match scene.</summary>
public sealed class StadiumPropLayoutAsset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public int Version { get; set; } = 1;
    public string AssetDirectory { get; set; } = "props";
    public List<StadiumPropPlacement> Placements { get; set; } = [];

    public static StadiumPropLayoutAsset Load(string layoutPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutPath);
        var layoutDirectory = Path.GetDirectoryName(Path.GetFullPath(layoutPath))
            ?? throw new InvalidDataException($"Stadium layout '{layoutPath}' has no parent directory.");
        var layout = JsonSerializer.Deserialize<StadiumPropLayoutAsset>(File.ReadAllText(layoutPath), JsonOptions)
            ?? throw new InvalidDataException($"Stadium layout '{layoutPath}' was empty.");
        var errors = layout.Validate(layoutDirectory);
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid stadium layout '{layoutPath}': {string.Join(" ", errors)}");
        return layout;
    }

    public List<string> Validate(string layoutDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutDirectory);
        var errors = new List<string>();
        if (Version != 1)
            errors.Add("Stadium layout version must be 1.");
        if (Placements is null || Placements.Count == 0)
        {
            errors.Add("Stadium layout must contain at least one placement.");
            return errors;
        }

        var root = Path.GetFullPath(layoutDirectory);
        var assetRoot = ResolveInside(root, AssetDirectory);
        if (assetRoot is null || !Directory.Exists(assetRoot))
        {
            errors.Add("Stadium layout assetDirectory must be a directory inside the layout folder.");
            return errors;
        }

        foreach (var placement in Placements)
        {
            if (placement is null)
            {
                errors.Add("Stadium layout cannot contain a missing placement.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(placement.Asset) || Path.IsPathFullyQualified(placement.Asset) ||
                !string.Equals(Path.GetExtension(placement.Asset), ".glb", StringComparison.OrdinalIgnoreCase))
                errors.Add("Every stadium placement must reference a relative GLB asset filename.");
            else
            {
                var assetPath = ResolveInside(assetRoot, placement.Asset);
                if (assetPath is null || !File.Exists(assetPath))
                    errors.Add($"Stadium placement asset '{placement.Asset}' must exist inside '{AssetDirectory}'.");
            }
            if (placement.Position is null || !placement.Position.IsFinite())
                errors.Add($"Stadium placement '{placement.Asset}' must have a finite position.");
            if (!float.IsFinite(placement.RotationYDegrees))
                errors.Add($"Stadium placement '{placement.Asset}' rotation must be finite degrees.");
            if (!float.IsFinite(placement.Scale) || placement.Scale is < 0.1f or > 10f)
                errors.Add($"Stadium placement '{placement.Asset}' scale must be between 0.1 and 10.");
        }
        return errors;
    }

    public string GetAssetPath(string layoutPath, string assetFile)
    {
        var layoutDirectory = Path.GetDirectoryName(Path.GetFullPath(layoutPath))
            ?? throw new InvalidDataException($"Stadium layout '{layoutPath}' has no parent directory.");
        var assetRoot = ResolveInside(layoutDirectory, AssetDirectory)
            ?? throw new InvalidDataException("Stadium asset directory must remain inside the layout folder.");
        return ResolveInside(assetRoot, assetFile)
            ?? throw new InvalidDataException($"Stadium prop '{assetFile}' must remain inside the asset directory.");
    }

    private static string? ResolveInside(string parent, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
            return null;
        var fullPath = Path.GetFullPath(Path.Combine(parent, relativePath));
        var relative = Path.GetRelativePath(parent, fullPath);
        return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? null
            : fullPath;
    }
}

public sealed class StadiumPropPlacement
{
    public string Asset { get; set; } = string.Empty;
    public Vector3Data Position { get; set; } = new();
    public float RotationYDegrees { get; set; }
    public float Scale { get; set; } = 1f;
}
