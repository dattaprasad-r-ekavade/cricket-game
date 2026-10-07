using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using SuperCricket.Content;

namespace SuperCricket.Game.Rendering;

/// <summary>GPU-skinned meshes drawn with MonoGame's built-in SkinnedEffect.</summary>
public sealed class SkinnedPlayerRenderer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SkinnedEffect _effect;
    private readonly Texture2D _whiteTexture;
    private readonly Texture2D[] _baseColorTextures;
    private readonly SamplerState[] _baseColorSamplers;
    private readonly List<MeshBuffers> _meshes;
    private readonly Vector3 _primaryKitBaseColor;
    private readonly Vector3 _accentKitBaseColor;

    public SkinnedPlayerRenderer(GraphicsDevice graphicsDevice, PlayerAsset asset)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new SkinnedEffect(graphicsDevice)
        {
            PreferPerPixelLighting = true,
            DiffuseColor = Vector3.One,
            SpecularColor = new Vector3(0.12f, 0.12f, 0.12f),
            SpecularPower = 18f
        };
        _effect.EnableDefaultLighting();
        _effect.AmbientLightColor = new Vector3(0.72f, 0.72f, 0.72f);
        _effect.DirectionalLight0.DiffuseColor = Vector3.One;
        _whiteTexture = new Texture2D(graphicsDevice, 1, 1);
        _whiteTexture.SetData([Color.White]);
        _effect.Texture = _whiteTexture;
        _baseColorTextures = asset.Textures.Select(texture =>
        {
            using var stream = new MemoryStream(texture.Content, writable: false);
            return Texture2D.FromStream(graphicsDevice, stream);
        }).ToArray();
        _baseColorSamplers = asset.Textures.Select(CreateSamplerState).ToArray();
        _primaryKitBaseColor = FindDiffuseColor(asset, "Shirt") ?? Vector3.One;
        _accentKitBaseColor = FindDiffuseColor(asset, "Player Detail | Jersey Collar") ?? _primaryKitBaseColor;
        _meshes = asset.Meshes
            .GroupBy(mesh => (
                mesh.DiffuseColor.X,
                mesh.DiffuseColor.Y,
                mesh.DiffuseColor.Z,
                mesh.BaseColorTextureIndex,
                KitColorSlot: GetKitColorSlot(mesh.Name)))
            .Select(group =>
            {
                var baseColor = group.Key.KitColorSlot switch
                {
                    KitColorSlot.Primary => _primaryKitBaseColor,
                    KitColorSlot.Accent => _accentKitBaseColor,
                    _ => Vector3.One
                };
                return new MeshBuffers(
                    graphicsDevice,
                    group.ToList(),
                    new Vector3(group.Key.X, group.Key.Y, group.Key.Z),
                    group.Key.BaseColorTextureIndex,
                    group.Key.KitColorSlot,
                    baseColor);
            })
            .ToList();
    }

    public int MaterialBatchCount => _meshes.Count;

    public void Draw(
        Matrix world,
        Matrix view,
        Matrix projection,
        Matrix[] skinMatrices,
        Vector3 primaryKitColor,
        Vector3 accentKitColor)
    {
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.SetBoneTransforms(skinMatrices);
        var previousSampler = _graphicsDevice.SamplerStates[0];
        try
        {
            foreach (var mesh in _meshes)
            {
                var textureIndex = mesh.BaseColorTextureIndex;
                _effect.Texture = textureIndex >= 0 ? _baseColorTextures[textureIndex] : _whiteTexture;
                _effect.DiffuseColor = mesh.KitColorSlot switch
                {
                    KitColorSlot.Primary => ClampColor(mesh.TeamColorRatio * primaryKitColor),
                    KitColorSlot.Accent => ClampColor(mesh.TeamColorRatio * accentKitColor),
                    _ => mesh.DiffuseColor
                };
                _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
                _graphicsDevice.Indices = mesh.IndexBuffer;

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.SamplerStates[0] = textureIndex >= 0
                        ? _baseColorSamplers[textureIndex]
                        : SamplerState.LinearWrap;
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        mesh.IndexCount / 3);
                }
            }
        }
        finally
        {
            _graphicsDevice.SetVertexBuffer(null);
            _graphicsDevice.Indices = null;
            _graphicsDevice.SamplerStates[0] = previousSampler;
        }
    }

    private static Vector3? FindDiffuseColor(PlayerAsset asset, string meshName)
    {
        var mesh = asset.Meshes.Find(candidate => string.Equals(candidate.Name, meshName, StringComparison.OrdinalIgnoreCase));
        return mesh is null ? null : new Vector3(mesh.DiffuseColor.X, mesh.DiffuseColor.Y, mesh.DiffuseColor.Z);
    }

    private static KitColorSlot GetKitColorSlot(string meshName)
    {
        if (string.Equals(meshName, "Shirt", StringComparison.OrdinalIgnoreCase) ||
            meshName.StartsWith("Forearm ", StringComparison.OrdinalIgnoreCase))
            return KitColorSlot.Primary;
        if (meshName.Contains("Jersey Collar", StringComparison.OrdinalIgnoreCase) ||
            meshName.Contains("Sleeve Band", StringComparison.OrdinalIgnoreCase) ||
            meshName.Contains("Chest Crest Mark", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(meshName, "Player Detail | Crest Stripe", StringComparison.OrdinalIgnoreCase))
            return KitColorSlot.Accent;
        return KitColorSlot.None;
    }

    private static Vector3 ClampColor(Vector3 value) => Vector3.Clamp(value, Vector3.Zero, Vector3.One);

    private static float ColorRatio(float value, float reference) => reference > 0.001f ? value / reference : 1f;

    private enum KitColorSlot
    {
        None,
        Primary,
        Accent
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes)
            mesh.Dispose();
        foreach (var texture in _baseColorTextures)
            texture.Dispose();
        foreach (var sampler in _baseColorSamplers)
            sampler.Dispose();
        _whiteTexture.Dispose();
        _effect.Dispose();
    }

    internal static SamplerState CreateSamplerState(PlayerTextureData texture) => new()
    {
        Filter = ToTextureFilter(texture.MinFilter, texture.MagFilter),
        AddressU = ToAddressMode(texture.WrapU),
        AddressV = ToAddressMode(texture.WrapV),
        AddressW = TextureAddressMode.Clamp
    };

    private static TextureAddressMode ToAddressMode(int mode) => mode switch
    {
        10497 => TextureAddressMode.Wrap,
        33071 => TextureAddressMode.Clamp,
        33648 => TextureAddressMode.Mirror,
        _ => throw new InvalidDataException($"Unsupported glTF texture wrap mode '{mode}'.")
    };

    private static TextureFilter ToTextureFilter(int minFilter, int magFilter)
    {
        var minLinear = minFilter is 9729 or 9985 or 9987;
        var mipLinear = minFilter is 9728 or 9729 or 9986 or 9987;
        var magLinear = magFilter == 9729;
        return (minLinear, magLinear, mipLinear) switch
        {
            (true, true, true) => TextureFilter.Linear,
            (false, false, false) => TextureFilter.Point,
            (true, false, true) => TextureFilter.MinLinearMagPointMipLinear,
            (true, false, false) => TextureFilter.MinLinearMagPointMipPoint,
            (false, true, true) => TextureFilter.MinPointMagLinearMipLinear,
            (false, true, false) => TextureFilter.MinPointMagLinearMipPoint,
            (true, true, false) => TextureFilter.LinearMipPoint,
            (false, false, true) => TextureFilter.PointMipLinear
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SkinnedVertex : IVertexType
    {
        public static readonly VertexDeclaration Declaration = new(
            52,
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0),
            new VertexElement(48, VertexElementFormat.Byte4, VertexElementUsage.BlendIndices, 0));

        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public Vector4 BoneWeights;
        public Byte4 BoneIndices;

        VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }

    private sealed class MeshBuffers : IDisposable
    {
        public MeshBuffers(
            GraphicsDevice graphicsDevice,
            IReadOnlyList<PlayerMeshData> meshes,
            Vector3 diffuseColor,
            int baseColorTextureIndex,
            KitColorSlot kitColorSlot,
            Vector3 referenceColor)
        {
            var vertexCount = meshes.Sum(mesh => mesh.Positions.Length / 3);
            var indexCount = meshes.Sum(mesh => mesh.Indices.Length);
            var vertices = new SkinnedVertex[vertexCount];
            var indices = new int[indexCount];
            var vertexBase = 0;
            var indexOffset = 0;
            foreach (var mesh in meshes)
            {
                var meshVertexCount = mesh.Positions.Length / 3;
                for (var vertexIndex = 0; vertexIndex < meshVertexCount; vertexIndex++)
                {
                    var vectorOffset = vertexIndex * 3;
                    var uvOffset = vertexIndex * 2;
                    var influenceOffset = vertexIndex * 4;
                    vertices[vertexBase + vertexIndex] = new SkinnedVertex
                    {
                        Position = new Vector3(mesh.Positions[vectorOffset], mesh.Positions[vectorOffset + 1], mesh.Positions[vectorOffset + 2]),
                        Normal = new Vector3(mesh.Normals[vectorOffset], mesh.Normals[vectorOffset + 1], mesh.Normals[vectorOffset + 2]),
                        TextureCoordinate = new Vector2(mesh.TextureCoordinates[uvOffset], mesh.TextureCoordinates[uvOffset + 1]),
                        BoneWeights = new Vector4(mesh.BoneWeights[influenceOffset], mesh.BoneWeights[influenceOffset + 1], mesh.BoneWeights[influenceOffset + 2], mesh.BoneWeights[influenceOffset + 3]),
                        BoneIndices = new Byte4(mesh.BoneIndices[influenceOffset], mesh.BoneIndices[influenceOffset + 1], mesh.BoneIndices[influenceOffset + 2], mesh.BoneIndices[influenceOffset + 3])
                    };
                }

                for (var meshIndex = 0; meshIndex < mesh.Indices.Length; meshIndex++)
                    indices[indexOffset + meshIndex] = mesh.Indices[meshIndex] + vertexBase;

                vertexBase += meshVertexCount;
                indexOffset += mesh.Indices.Length;
            }

            VertexBuffer = new VertexBuffer(graphicsDevice, SkinnedVertex.Declaration, vertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);
            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indexCount, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);
            IndexCount = indexCount;
            DiffuseColor = diffuseColor;
            BaseColorTextureIndex = baseColorTextureIndex;
            KitColorSlot = kitColorSlot;
            TeamColorRatio = new Vector3(
                ColorRatio(diffuseColor.X, referenceColor.X),
                ColorRatio(diffuseColor.Y, referenceColor.Y),
                ColorRatio(diffuseColor.Z, referenceColor.Z));
        }

        public VertexBuffer VertexBuffer { get; }
        public IndexBuffer IndexBuffer { get; }
        public int IndexCount { get; }
        public Vector3 DiffuseColor { get; }
        public int BaseColorTextureIndex { get; }
        public KitColorSlot KitColorSlot { get; }
        public Vector3 TeamColorRatio { get; }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
        }
    }
}
