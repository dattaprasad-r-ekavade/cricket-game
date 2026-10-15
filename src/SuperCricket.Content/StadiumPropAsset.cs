using System.Numerics;
using SharpGLTF.Schema2;

namespace SuperCricket.Content;

/// <summary>Static triangle meshes and material colors imported from a modular WP20 GLB.</summary>
public sealed class StadiumPropAsset
{
    public string Name { get; set; } = string.Empty;
    public List<StadiumPropMesh> Meshes { get; set; } = [];

    public static StadiumPropAsset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Stadium prop '{path}' must be a GLB (.glb).");
        var model = ModelRoot.Load(path);
        if (model.LogicalSkins.Count > 0)
            throw new InvalidDataException($"Static stadium prop '{path}' must not contain a skin.");

        var meshes = new List<StadiumPropMesh>();
        foreach (var node in model.LogicalNodes.Where(candidate => candidate.Mesh is not null))
        {
            if (!Matrix4x4.Invert(node.WorldMatrix, out var inverseWorld))
                throw new InvalidDataException($"Stadium prop node '{node.Name}' in '{path}' has a non-invertible transform.");
            var normalMatrix = Matrix4x4.Transpose(inverseWorld);
            foreach (var primitive in node.Mesh!.Primitives)
            {
                if (primitive.DrawPrimitiveType.ToString() != "TRIANGLES")
                    throw new InvalidDataException($"Stadium prop primitive '{node.Name}' must use triangle-list topology.");
                var positionAccessor = primitive.GetVertexAccessor("POSITION")
                    ?? throw new InvalidDataException($"Stadium prop primitive '{node.Name}' is missing positions.");
                var normalAccessor = primitive.GetVertexAccessor("NORMAL")
                    ?? throw new InvalidDataException($"Stadium prop primitive '{node.Name}' is missing normals.");
                var sourcePositions = positionAccessor.AsVector3Array().ToArray();
                var sourceNormals = normalAccessor.AsVector3Array().ToArray();
                if (sourcePositions.Length == 0 || sourceNormals.Length != sourcePositions.Length)
                    throw new InvalidDataException($"Stadium prop primitive '{node.Name}' has mismatched position and normal data.");

                var material = primitive.Material;
                if (material is not null && material.Alpha.ToString() != "OPAQUE")
                    throw new InvalidDataException($"Stadium prop material '{material.Name}' uses unsupported alpha mode '{material.Alpha}'.");
                var baseColor = material?.FindChannel("BaseColor")?.Color ?? Vector4.One;
                if (!float.IsFinite(baseColor.X) || !float.IsFinite(baseColor.Y) ||
                    !float.IsFinite(baseColor.Z) || !float.IsFinite(baseColor.W) || baseColor.W < 0.999f)
                    throw new InvalidDataException($"Stadium prop primitive '{node.Name}' must use an opaque finite base color.");

                var positions = sourcePositions.Select(position => Vector3.Transform(position, node.WorldMatrix)).ToArray();
                var normals = sourceNormals.Select(normal =>
                {
                    var transformed = Vector3.TransformNormal(normal, normalMatrix);
                    return transformed.LengthSquared() > 0.000001f
                        ? Vector3.Normalize(transformed)
                        : Vector3.UnitY;
                }).ToArray();
                var indices = primitive.GetTriangleIndices()
                    .SelectMany(triangle => new[] { triangle.Item1, triangle.Item2, triangle.Item3 })
                    .ToArray();
                if (indices.Length == 0 || indices.Length % 3 != 0 || indices.Any(index => index < 0 || index >= positions.Length))
                    throw new InvalidDataException($"Stadium prop primitive '{node.Name}' has invalid triangle indices.");

                meshes.Add(new StadiumPropMesh
                {
                    Name = node.Name ?? string.Empty,
                    DiffuseColor = new Vector3(baseColor.X, baseColor.Y, baseColor.Z),
                    Positions = positions,
                    Normals = normals,
                    Indices = indices
                });
            }
        }

        if (meshes.Count == 0)
            throw new InvalidDataException($"Stadium prop '{path}' contains no static triangle meshes.");
        return new StadiumPropAsset { Name = Path.GetFileNameWithoutExtension(path), Meshes = meshes };
    }
}

public sealed class StadiumPropMesh
{
    public string Name { get; set; } = string.Empty;
    public Vector3 DiffuseColor { get; set; } = Vector3.One;
    public Vector3[] Positions { get; set; } = [];
    public Vector3[] Normals { get; set; } = [];
    public int[] Indices { get; set; } = [];
}
