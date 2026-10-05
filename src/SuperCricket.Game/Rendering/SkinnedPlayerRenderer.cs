using System;
using System.Collections.Generic;
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
    private readonly List<MeshBuffers> _meshes;

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
        _meshes = asset.Meshes.Select(mesh => new MeshBuffers(graphicsDevice, mesh)).ToList();
    }

    public void Draw(Matrix world, Matrix view, Matrix projection, Matrix[] skinMatrices)
    {
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.SetBoneTransforms(skinMatrices);

        foreach (var mesh in _meshes)
        {
            _effect.DiffuseColor = mesh.DiffuseColor;
            _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
            _graphicsDevice.Indices = mesh.IndexBuffer;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    0,
                    0,
                    mesh.IndexCount / 3);
            }
        }

        _graphicsDevice.SetVertexBuffer(null);
        _graphicsDevice.Indices = null;
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes)
            mesh.Dispose();
        _whiteTexture.Dispose();
        _effect.Dispose();
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
        public MeshBuffers(GraphicsDevice graphicsDevice, PlayerMeshData mesh)
        {
            var vertexCount = mesh.Positions.Length / 3;
            var vertices = new SkinnedVertex[vertexCount];
            for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                var vectorOffset = vertexIndex * 3;
                var uvOffset = vertexIndex * 2;
                var influenceOffset = vertexIndex * 4;
                vertices[vertexIndex] = new SkinnedVertex
                {
                    Position = new Vector3(mesh.Positions[vectorOffset], mesh.Positions[vectorOffset + 1], mesh.Positions[vectorOffset + 2]),
                    Normal = new Vector3(mesh.Normals[vectorOffset], mesh.Normals[vectorOffset + 1], mesh.Normals[vectorOffset + 2]),
                    TextureCoordinate = new Vector2(mesh.TextureCoordinates[uvOffset], mesh.TextureCoordinates[uvOffset + 1]),
                    BoneWeights = new Vector4(mesh.BoneWeights[influenceOffset], mesh.BoneWeights[influenceOffset + 1], mesh.BoneWeights[influenceOffset + 2], mesh.BoneWeights[influenceOffset + 3]),
                    BoneIndices = new Byte4(mesh.BoneIndices[influenceOffset], mesh.BoneIndices[influenceOffset + 1], mesh.BoneIndices[influenceOffset + 2], mesh.BoneIndices[influenceOffset + 3])
                };
            }

            VertexBuffer = new VertexBuffer(graphicsDevice, SkinnedVertex.Declaration, vertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);
            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, mesh.Indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(mesh.Indices);
            IndexCount = mesh.Indices.Length;
            DiffuseColor = new Vector3(mesh.DiffuseColor.X, mesh.DiffuseColor.Y, mesh.DiffuseColor.Z);
        }

        public VertexBuffer VertexBuffer { get; }
        public IndexBuffer IndexBuffer { get; }
        public int IndexCount { get; }
        public Vector3 DiffuseColor { get; }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
        }
    }
}
