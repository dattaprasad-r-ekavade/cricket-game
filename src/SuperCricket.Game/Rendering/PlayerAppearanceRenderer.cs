using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Content;

namespace SuperCricket.Game.Rendering;

/// <summary>Loads the WP19 bodies and selects their runtime LOD using camera distance.</summary>
public sealed class PlayerAppearanceRenderer : IDisposable
{
    private readonly PlayerVariantCatalog _catalog;
    private readonly Dictionary<string, SkinnedPlayerRenderer[]> _renderers = new(StringComparer.OrdinalIgnoreCase);

    public PlayerAppearanceRenderer(GraphicsDevice graphicsDevice, string manifestPath, PlayerAsset animationTemplate)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(animationTemplate);
        _catalog = PlayerVariantCatalog.Load(manifestPath);
        var assetDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
            ?? throw new InvalidDataException($"Player variant manifest '{manifestPath}' has no parent directory.");

        try
        {
            foreach (var variant in _catalog.Variants)
            {
                var lodRenderers = new SkinnedPlayerRenderer[3];
                _renderers.Add(variant.Id, lodRenderers);
                for (var lod = 0; lod < lodRenderers.Length; lod++)
                {
                    var path = _catalog.GetGlbPath(assetDirectory, variant.Id, lod);
                    var asset = PlayerAsset.LoadSkinnedVariant(path, animationTemplate);
                    _catalog.ApplyTeamColorMasks(asset, assetDirectory);
                    lodRenderers[lod] = new SkinnedPlayerRenderer(graphicsDevice, asset);
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int MaterialBatchCount => _renderers["balanced"][0].MaterialBatchCount;

    public int SelectLod(float cameraDistanceMeters) => _catalog.SelectLod(cameraDistanceMeters);

    public void Draw(
        string variantId,
        Matrix world,
        Vector3 cameraPosition,
        Matrix view,
        Matrix projection,
        Matrix[] skinMatrices,
        Vector3 primaryKitColor,
        Vector3 accentKitColor)
    {
        var distance = Vector3.Distance(cameraPosition, world.Translation);
        var lod = _catalog.SelectLod(distance);
        if (!_renderers.TryGetValue(variantId, out var lodRenderers))
            throw new KeyNotFoundException($"Unknown player variant '{variantId}'.");
        lodRenderers[lod].Draw(world, view, projection, skinMatrices, primaryKitColor, accentKitColor);
    }

    public void Dispose()
    {
        foreach (var lodRenderers in _renderers.Values)
        {
            foreach (var renderer in lodRenderers)
                renderer?.Dispose();
        }
        _renderers.Clear();
    }
}
