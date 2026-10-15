using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Content;

namespace SuperCricket.Game.Rendering;

/// <summary>Draws the validated WP20 static GLB props at their authored field placements.</summary>
public sealed class StadiumPropRenderer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly List<PropInstance> _instances = [];
    private readonly List<GpuMesh> _meshes = [];
    private bool _disposed;

    public int PlacementCount => _instances.Count;
    public int MeshCount => _meshes.Count;

    public StadiumPropRenderer(GraphicsDevice graphicsDevice, string layoutPath)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _graphicsDevice = graphicsDevice;
        var layout = StadiumPropLayoutAsset.Load(layoutPath);
        var assets = new Dictionary<string, List<GpuMesh>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var placement in layout.Placements)
            {
                if (!assets.TryGetValue(placement.Asset, out var meshes))
                {
                    var path = layout.GetAssetPath(layoutPath, placement.Asset);
                    meshes = StadiumPropAsset.Load(path).Meshes.Select(CreateGpuMesh).ToList();
                    assets.Add(placement.Asset, meshes);
                    _meshes.AddRange(meshes);
                }

                _instances.Add(new PropInstance(
                    meshes,
                    Matrix.CreateScale(placement.Scale)
                        * Matrix.CreateRotationY(MathHelper.ToRadians(placement.RotationYDegrees))
                        * Matrix.CreateTranslation(placement.Position.X, placement.Position.Y, placement.Position.Z)));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Draw(BasicEffect effect, Matrix view, Matrix projection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        effect.View = view;
        effect.Projection = projection;
        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;
        effect.LightingEnabled = true;
        foreach (var instance in _instances)
        {
            effect.World = instance.World;
            foreach (var mesh in instance.Meshes)
            {
                _graphicsDevice.SetVertexBuffer(mesh.Vertices);
                _graphicsDevice.Indices = mesh.Indices;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        mesh.IndexCount / 3);
                }
            }
        }
        _graphicsDevice.SetVertexBuffer(null);
        _graphicsDevice.Indices = null;
        effect.World = Matrix.Identity;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        foreach (var mesh in _meshes)
        {
            mesh.Vertices.Dispose();
            mesh.Indices.Dispose();
        }
        _meshes.Clear();
        _instances.Clear();
        _disposed = true;
    }

    private GpuMesh CreateGpuMesh(StadiumPropMesh mesh)
    {
        if (mesh.Positions.Length != mesh.Normals.Length || mesh.Positions.Length == 0 || mesh.Indices.Length == 0)
            throw new InvalidDataException($"Stadium prop mesh '{mesh.Name}' has invalid vertex or index data.");
        var diffuse = mesh.DiffuseColor;
        var color = new Color(
            ToByte(diffuse.X),
            ToByte(diffuse.Y),
            ToByte(diffuse.Z));
        var vertices = new VertexPositionColorNormal[mesh.Positions.Length];
        for (var index = 0; index < vertices.Length; index++)
        {
            var position = mesh.Positions[index];
            var normal = mesh.Normals[index];
            vertices[index] = new VertexPositionColorNormal(
                new Microsoft.Xna.Framework.Vector3(position.X, position.Y, position.Z),
                color,
                new Microsoft.Xna.Framework.Vector3(normal.X, normal.Y, normal.Z));
        }

        var vertexBuffer = new VertexBuffer(
            _graphicsDevice,
            VertexPositionColorNormal.VertexDeclaration,
            vertices.Length,
            BufferUsage.WriteOnly);
        vertexBuffer.SetData(vertices);
        var indexBuffer = new IndexBuffer(
            _graphicsDevice,
            IndexElementSize.ThirtyTwoBits,
            mesh.Indices.Length,
            BufferUsage.WriteOnly);
        indexBuffer.SetData(mesh.Indices);
        return new GpuMesh(vertexBuffer, indexBuffer, vertices.Length, mesh.Indices.Length);
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    private sealed record PropInstance(IReadOnlyList<GpuMesh> Meshes, Matrix World);
    private sealed record GpuMesh(VertexBuffer Vertices, IndexBuffer Indices, int VertexCount, int IndexCount);
}
