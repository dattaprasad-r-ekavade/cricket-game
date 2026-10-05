using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

/// <summary>Blender-exported skinned player mesh, skeleton, and sampled animation clips.</summary>
public sealed class PlayerAsset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public int Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string CoordinateSystem { get; set; } = "right-handed-y-up-metres";
    public List<PlayerBoneData> Bones { get; set; } = [];
    public List<PlayerMeshData> Meshes { get; set; } = [];
    public List<PlayerAnimationData> Animations { get; set; } = [];

    public static PlayerAsset Load(string path)
    {
        var json = File.ReadAllText(path);
        var asset = JsonSerializer.Deserialize<PlayerAsset>(json, JsonOptions)
            ?? throw new InvalidDataException($"Player asset '{path}' was empty.");
        var errors = asset.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"Invalid player asset '{path}': {string.Join(" ", errors)}");
        }

        return asset;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add($"Unsupported player asset version {Version}; expected 1.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Player name must not be empty.");
        if (CoordinateSystem != "right-handed-y-up-metres") errors.Add("Coordinate system must be right-handed-y-up-metres.");
        if (Bones is null || Bones.Count is < 1 or > 72)
        {
            errors.Add("Player must contain between 1 and 72 bones.");
            return errors;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var rootCount = 0;
        for (var index = 0; index < Bones.Count; index++)
        {
            var bone = Bones[index];
            if (bone is null)
            {
                errors.Add($"Bone {index} is missing.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(bone.Name) || !names.Add(bone.Name))
                errors.Add($"Bone {index} must have a unique, non-empty name.");
            if (bone.ParentIndex == -1) rootCount++;
            else if (bone.ParentIndex < 0 || bone.ParentIndex >= index)
                errors.Add($"Bone '{bone.Name}' must refer to an earlier parent or use -1 for the root.");
            if (bone.BindPose is null || !bone.BindPose.IsFinite())
            {
                errors.Add($"Bone '{bone.Name}' has an invalid bind pose.");
            }
            else if (!Matrix4x4.Invert(bone.BindPose.ToNumericsMatrix(), out _))
            {
                errors.Add($"Bone '{bone.Name}' has a non-invertible bind pose.");
            }
        }
        if (rootCount != 1) errors.Add("Skeleton must have exactly one root bone.");

        if (Meshes is null || Meshes.Count == 0)
        {
            errors.Add("Player must contain at least one mesh.");
        }
        else
        {
            for (var meshIndex = 0; meshIndex < Meshes.Count; meshIndex++)
                ValidateMesh(Meshes[meshIndex], meshIndex, Bones.Count, errors);
        }

        if (Animations is null || Animations.Count < 2)
        {
            errors.Add("Player must contain at least two animation clips.");
        }
        else
        {
            var clipNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in Animations)
            {
                if (clip is null)
                {
                    errors.Add("An animation clip is missing.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(clip.Name) || !clipNames.Add(clip.Name))
                    errors.Add("Animation clip names must be unique and non-empty.");
                if (!float.IsFinite(clip.DurationSeconds) || clip.DurationSeconds <= 0f)
                    errors.Add($"Clip '{clip.Name}' duration must be positive.");
                if (clip.Events is null)
                {
                    errors.Add($"Clip '{clip.Name}' events must not be missing.");
                }
                else
                {
                    var eventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var animationEvent in clip.Events)
                    {
                        if (animationEvent is null)
                        {
                            errors.Add($"Clip '{clip.Name}' contains a missing animation event.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(animationEvent.Name) || !eventNames.Add(animationEvent.Name))
                            errors.Add($"Clip '{clip.Name}' event names must be unique and non-empty.");
                        if (!float.IsFinite(animationEvent.TimeSeconds) || animationEvent.TimeSeconds < 0f ||
                            animationEvent.TimeSeconds > clip.DurationSeconds)
                            errors.Add($"Clip '{clip.Name}' event '{animationEvent.Name}' time must be within the clip duration.");
                    }
                }
                if (clip.Samples is null || clip.Samples.Count < 2)
                {
                    errors.Add($"Clip '{clip.Name}' must contain at least two pose samples.");
                    continue;
                }

                var previousTime = -1f;
                foreach (var sample in clip.Samples)
                {
                    if (sample is null || !float.IsFinite(sample.TimeSeconds) || sample.TimeSeconds <= previousTime ||
                        sample.TimeSeconds < 0f || sample.TimeSeconds > clip.DurationSeconds)
                    {
                        errors.Add($"Clip '{clip.Name}' pose sample times must increase within the clip duration.");
                        break;
                    }
                    if (sample.RootMotion is null || !sample.RootMotion.IsFinite())
                    {
                        errors.Add($"Clip '{clip.Name}' sample at {sample.TimeSeconds:0.###} s must have finite root motion.");
                        break;
                    }
                    if (sample.Bones is null || sample.Bones.Count != Bones.Count || sample.Bones.Any(pose => pose is null || !pose.IsFinite()))
                    {
                        errors.Add($"Clip '{clip.Name}' sample at {sample.TimeSeconds:0.###} s must contain one valid pose per bone.");
                        break;
                    }
                    previousTime = sample.TimeSeconds;
                }
            }
        }

        return errors;
    }

    private static void ValidateMesh(PlayerMeshData? mesh, int meshIndex, int boneCount, List<string> errors)
    {
        if (mesh is null)
        {
            errors.Add($"Mesh {meshIndex} is missing.");
            return;
        }
        var name = string.IsNullOrWhiteSpace(mesh.Name) ? meshIndex.ToString() : mesh.Name;
        if (mesh.DiffuseColor is null || !mesh.DiffuseColor.IsFinite() ||
            mesh.DiffuseColor.X is < 0f or > 1f || mesh.DiffuseColor.Y is < 0f or > 1f || mesh.DiffuseColor.Z is < 0f or > 1f)
            errors.Add($"Mesh '{name}' diffuse color must contain RGB values from 0 to 1.");
        if (mesh.Positions is null || mesh.Positions.Length == 0 || mesh.Positions.Length % 3 != 0)
        {
            errors.Add($"Mesh '{name}' positions must contain complete XYZ vertices.");
            return;
        }

        var vertexCount = mesh.Positions.Length / 3;
        if (mesh.Positions.Any(value => !float.IsFinite(value))) errors.Add($"Mesh '{name}' contains a non-finite position.");
        if (mesh.Normals is null || mesh.Normals.Length != vertexCount * 3 || mesh.Normals.Any(value => !float.IsFinite(value)))
            errors.Add($"Mesh '{name}' must have one finite normal per vertex.");
        if (mesh.TextureCoordinates is null || mesh.TextureCoordinates.Length != vertexCount * 2 || mesh.TextureCoordinates.Any(value => !float.IsFinite(value)))
            errors.Add($"Mesh '{name}' must have one finite UV coordinate per vertex.");
        if (mesh.BoneIndices is null || mesh.BoneIndices.Length != vertexCount * 4 ||
            mesh.BoneWeights is null || mesh.BoneWeights.Length != vertexCount * 4)
        {
            errors.Add($"Mesh '{name}' must have four bone influences per vertex.");
        }
        else
        {
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                var offset = vertex * 4;
                var sum = 0f;
                for (var influence = 0; influence < 4; influence++)
                {
                    var weight = mesh.BoneWeights[offset + influence];
                    if (mesh.BoneIndices[offset + influence] < 0 || mesh.BoneIndices[offset + influence] >= boneCount ||
                        !float.IsFinite(weight) || weight < 0f)
                    {
                        errors.Add($"Mesh '{name}' vertex {vertex} has an invalid bone influence.");
                        break;
                    }
                    sum += weight;
                }
                if (MathF.Abs(sum - 1f) > 0.002f)
                    errors.Add($"Mesh '{name}' vertex {vertex} bone weights must sum to 1.");
            }
        }
        if (mesh.Indices is null || mesh.Indices.Length == 0 || mesh.Indices.Length % 3 != 0 ||
            mesh.Indices.Any(index => index < 0 || index >= vertexCount))
            errors.Add($"Mesh '{name}' must have valid triangle indices.");
    }
}

public sealed class PlayerBoneData
{
    public string Name { get; set; } = string.Empty;
    public int ParentIndex { get; set; } = -1;
    public TransformData BindPose { get; set; } = new();
}

public sealed class PlayerMeshData
{
    public string Name { get; set; } = string.Empty;
    public Vector3Data DiffuseColor { get; set; } = new() { X = 1f, Y = 1f, Z = 1f };
    public float[] Positions { get; set; } = [];
    public float[] Normals { get; set; } = [];
    public float[] TextureCoordinates { get; set; } = [];
    public int[] BoneIndices { get; set; } = [];
    public float[] BoneWeights { get; set; } = [];
    public int[] Indices { get; set; } = [];
}

public sealed class PlayerAnimationData
{
    public string Name { get; set; } = string.Empty;
    public float DurationSeconds { get; set; }
    public List<PlayerAnimationEventData> Events { get; set; } = [];
    public List<PlayerPoseSampleData> Samples { get; set; } = [];
}

public sealed class PlayerAnimationEventData
{
    public string Name { get; set; } = string.Empty;
    public float TimeSeconds { get; set; }
}

public sealed class PlayerPoseSampleData
{
    public float TimeSeconds { get; set; }
    public Vector3Data RootMotion { get; set; } = new();
    public List<TransformData> Bones { get; set; } = [];
}
