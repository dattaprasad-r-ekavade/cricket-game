using System.Numerics;
using System.Text.Json.Nodes;
using SharpGLTF.Schema2;

namespace SuperCricket.Content;

/// <summary>Loads the project's skinned-player GLB profile into the shared runtime asset model.</summary>
internal static class PlayerGlbLoader
{
    private const int MetadataVersion = 1;
    private const float SampleRate = 30f;

    public static PlayerAsset Load(string path)
    {
        var model = ModelRoot.Load(path);
        if (model.LogicalSkins.Count != 1)
            throw new InvalidDataException($"Player GLB '{path}' must contain exactly one skin; found {model.LogicalSkins.Count}.");

        var skin = model.LogicalSkins[0];
        if (skin.JointsCount is < 1 or > 72)
            throw new InvalidDataException($"Player GLB '{path}' must contain between 1 and 72 skin joints; found {skin.JointsCount}.");
        if (skin.InverseBindMatrices.Count != skin.JointsCount)
            throw new InvalidDataException($"Player GLB '{path}' must contain one inverse-bind matrix per skin joint.");

        var jointNodes = skin.Joints.ToArray();
        if (jointNodes.Any(node => string.IsNullOrWhiteSpace(node.Name)) ||
            jointNodes.Select(node => node.Name).Distinct(StringComparer.Ordinal).Count() != jointNodes.Length)
            throw new InvalidDataException($"Player GLB '{path}' must have uniquely named skin joints.");

        var originalJointIndices = jointNodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index);
        var orderedNodes = OrderJointsByHierarchy(jointNodes, originalJointIndices);
        var orderedJointIndices = orderedNodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index);
        var originalToOrdered = new int[jointNodes.Length];
        for (var originalIndex = 0; originalIndex < jointNodes.Length; originalIndex++)
            originalToOrdered[originalIndex] = orderedJointIndices[jointNodes[originalIndex]];

        var bones = new List<PlayerBoneData>(orderedNodes.Count);
        foreach (var node in orderedNodes)
        {
            var originalIndex = originalJointIndices[node];
            var inverseBind = skin.InverseBindMatrices[originalIndex];
            if (!Matrix4x4.Invert(inverseBind, out var bindPose))
                throw new InvalidDataException($"Player GLB joint '{node.Name}' has a non-invertible inverse-bind matrix.");

            var parentIndex = node.VisualParent is not null && orderedJointIndices.TryGetValue(node.VisualParent, out var foundParent)
                ? foundParent
                : -1;
            bones.Add(new PlayerBoneData
            {
                Name = node.Name,
                ParentIndex = parentIndex,
                BindPose = ToTransform(bindPose, $"bind pose for '{node.Name}'")
            });
        }

        foreach (var root in orderedNodes.Where(node => node.VisualParent is null || !originalJointIndices.ContainsKey(node.VisualParent)))
        {
            if (root.VisualParent is not null && !IsIdentity(root.VisualParent.WorldMatrix))
                throw new InvalidDataException($"Player GLB skeleton root '{root.Name}' has an unsupported non-identity scene parent.");
        }

        var firstMetadata = model.LogicalAnimations
            .Select(animation => GetMetadata(animation))
            .FirstOrDefault(metadata => metadata is not null)
            ?? throw new InvalidDataException($"Player GLB '{path}' has no Super Cricket animation metadata.");
        var assetName = ReadString(firstMetadata, "assetName");
        var coordinateSystem = ReadString(firstMetadata, "coordinateSystem");
        if (coordinateSystem != "right-handed-y-up-metres")
            throw new InvalidDataException($"Player GLB '{path}' uses unsupported coordinates '{coordinateSystem}'.");
        var animations = model.LogicalAnimations.Select(animation =>
            ImportAnimation(animation, orderedNodes, bones, assetName)).ToList();
        var textureImport = ImportBaseColorTextures(model, path);

        var asset = new PlayerAsset
        {
            Name = assetName,
            CoordinateSystem = coordinateSystem,
            PoseSpace = "local",
            Bones = bones,
            Textures = textureImport.Textures,
            Meshes = ImportMeshes(model, skin, originalToOrdered, textureImport.IndexMap, path),
            Animations = animations
        };
        var errors = asset.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid player GLB '{path}': {string.Join(" ", errors)}");
        return asset;
    }

    private static List<Node> OrderJointsByHierarchy(Node[] joints, IReadOnlyDictionary<Node, int> jointIndices)
    {
        var ordered = new List<Node>(joints.Length);
        var visiting = new HashSet<Node>();
        var visited = new HashSet<Node>();

        void Visit(Node node)
        {
            if (visited.Contains(node))
                return;
            if (!visiting.Add(node))
                throw new InvalidDataException($"Player GLB skeleton contains a hierarchy cycle at joint '{node.Name}'.");
            if (node.VisualParent is not null && jointIndices.ContainsKey(node.VisualParent))
                Visit(node.VisualParent);
            visiting.Remove(node);
            visited.Add(node);
            ordered.Add(node);
        }

        foreach (var joint in joints)
            Visit(joint);
        return ordered;
    }

    private static List<PlayerMeshData> ImportMeshes(
        ModelRoot model,
        Skin skin,
        IReadOnlyList<int> originalToOrdered,
        IReadOnlyDictionary<int, int> textureIndexMap,
        string path)
    {
        var meshes = new List<PlayerMeshData>();
        foreach (var node in model.LogicalNodes.Where(candidate => candidate.Mesh is not null))
        {
            if (!ReferenceEquals(node.Skin, skin))
                throw new InvalidDataException($"Player GLB node '{node.Name}' references a different skin than the player skeleton.");
            if (node.LocalMatrix != Matrix4x4.Identity)
                throw new InvalidDataException($"Player GLB mesh node '{node.Name}' must have an identity local transform; apply mesh transforms before export.");

            foreach (var primitive in node.Mesh!.Primitives)
            {
                if (primitive.DrawPrimitiveType.ToString() != "TRIANGLES")
                    throw new InvalidDataException($"Player GLB primitive '{node.Name}' must use triangle-list topology.");
                var positionAccessor = RequiredAccessor(primitive, "POSITION", node.Name);
                var normalAccessor = RequiredAccessor(primitive, "NORMAL", node.Name);
                var jointAccessor = RequiredAccessor(primitive, "JOINTS_0", node.Name);
                var weightAccessor = RequiredAccessor(primitive, "WEIGHTS_0", node.Name);

                var positions = positionAccessor.AsVector3Array().ToArray();
                var normals = normalAccessor.AsVector3Array().ToArray();
                var uvAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
                var uvs = uvAccessor is null
                    ? new Vector2[positions.Length]
                    : uvAccessor.AsVector2Array().ToArray();
                var jointValues = jointAccessor.AsVector4Array().ToArray();
                var weightValues = weightAccessor.AsVector4Array().ToArray();
                if (positions.Length == 0 || normals.Length != positions.Length || uvs.Length != positions.Length ||
                    jointValues.Length != positions.Length || weightValues.Length != positions.Length)
                    throw new InvalidDataException($"Player GLB primitive '{node.Name}' has inconsistent vertex attributes.");

                var materialInfo = ReadMaterial(model, primitive.Material?.LogicalIndex ?? -1, textureIndexMap, node.Name);
                if (materialInfo.TextureIndex >= 0 && uvAccessor is null)
                    throw new InvalidDataException($"Player GLB primitive '{node.Name}' uses a base-color texture but has no TEXCOORD_0 accessor.");

                var flatPositions = new float[positions.Length * 3];
                var flatNormals = new float[normals.Length * 3];
                var flatUvs = new float[uvs.Length * 2];
                var flatJoints = new int[jointValues.Length * 4];
                var flatWeights = new float[weightValues.Length * 4];
                for (var index = 0; index < positions.Length; index++)
                {
                    WriteVector3(flatPositions, index * 3, positions[index]);
                    WriteVector3(flatNormals, index * 3, normals[index]);
                    var uv = Vector2.Transform(uvs[index], materialInfo.UvTransform);
                    flatUvs[index * 2] = uv.X;
                    flatUvs[index * 2 + 1] = uv.Y;
                    WriteJointIndices(flatJoints, index * 4, jointValues[index], originalToOrdered, node.Name, index);
                    WriteVector4(flatWeights, index * 4, weightValues[index]);
                }

                var indices = primitive.GetTriangleIndices()
                    .SelectMany(triangle => new[] { triangle.Item1, triangle.Item2, triangle.Item3 })
                    .ToArray();
                if (indices.Length == 0)
                    throw new InvalidDataException($"Player GLB primitive '{node.Name}' has no triangles.");

                meshes.Add(new PlayerMeshData
                {
                    Name = node.Name,
                    DiffuseColor = materialInfo.DiffuseColor,
                    BaseColorTextureIndex = materialInfo.TextureIndex,
                    Positions = flatPositions,
                    Normals = flatNormals,
                    TextureCoordinates = flatUvs,
                    BoneIndices = flatJoints,
                    BoneWeights = flatWeights,
                    Indices = indices
                });
            }
        }

        if (meshes.Count == 0)
            throw new InvalidDataException($"Player GLB '{path}' contains no mesh nodes using its skin.");
        return meshes;
    }

    private static Accessor RequiredAccessor(MeshPrimitive primitive, string semantic, string meshName) =>
        primitive.GetVertexAccessor(semantic)
        ?? throw new InvalidDataException($"Player GLB mesh '{meshName}' is missing its {semantic} vertex accessor.");

    private static (List<PlayerTextureData> Textures, Dictionary<int, int> IndexMap) ImportBaseColorTextures(ModelRoot model, string path)
    {
        var sourceIndices = model.LogicalNodes
            .Where(node => node.Mesh is not null)
            .SelectMany(node => node.Mesh!.Primitives)
            .Select(primitive => primitive.Material?.FindChannel("BaseColor"))
            .Where(channel => channel.HasValue && channel.Value.Texture is not null)
            .Select(channel => channel!.Value.Texture!.LogicalIndex)
            .Distinct()
            .Order()
            .ToArray();
        var textures = new List<PlayerTextureData>(sourceIndices.Length);
        var indexMap = new Dictionary<int, int>(sourceIndices.Length);
        foreach (var sourceIndex in sourceIndices)
        {
            if (sourceIndex < 0 || sourceIndex >= model.LogicalTextures.Count)
                throw new InvalidDataException($"Player GLB '{path}' references an invalid base-color texture index {sourceIndex}.");
            var texture = model.LogicalTextures[sourceIndex];
            var image = texture.PrimaryImage
                ?? throw new InvalidDataException($"Player GLB '{path}' texture '{texture.Name}' has no primary image.");
            var content = image.Content;
            var mimeType = content.IsPng ? "image/png" : content.IsJpg ? "image/jpeg" : null;
            if (mimeType is null || content.IsEmpty)
                throw new InvalidDataException($"Player GLB '{path}' texture '{texture.Name}' must use an embedded PNG or JPEG image.");
            indexMap.Add(sourceIndex, textures.Count);
            textures.Add(new PlayerTextureData
            {
                MimeType = mimeType,
                Content = content.Content.ToArray(),
                MinFilter = ToMinFilter(texture.Sampler?.MinFilter ?? TextureMipMapFilter.DEFAULT),
                MagFilter = ToMagFilter(texture.Sampler?.MagFilter ?? TextureInterpolationFilter.DEFAULT),
                WrapU = ToWrapMode(texture.Sampler?.WrapS ?? TextureWrapMode.REPEAT),
                WrapV = ToWrapMode(texture.Sampler?.WrapT ?? TextureWrapMode.REPEAT)
            });
        }
        return (textures, indexMap);
    }

    private static int ToMinFilter(TextureMipMapFilter filter) => filter.ToString() switch
    {
        "DEFAULT" or "LINEAR_MIPMAP_LINEAR" => 9987,
        "NEAREST" => 9728,
        "LINEAR" => 9729,
        "NEAREST_MIPMAP_NEAREST" => 9984,
        "LINEAR_MIPMAP_NEAREST" => 9985,
        "NEAREST_MIPMAP_LINEAR" => 9986,
        _ => throw new InvalidDataException($"Unsupported glTF texture minification filter '{filter}'.")
    };

    private static int ToMagFilter(TextureInterpolationFilter filter) => filter.ToString() switch
    {
        "DEFAULT" or "LINEAR" => 9729,
        "NEAREST" => 9728,
        _ => throw new InvalidDataException($"Unsupported glTF texture magnification filter '{filter}'.")
    };

    private static int ToWrapMode(TextureWrapMode mode) => mode.ToString() switch
    {
        "REPEAT" => 10497,
        "CLAMP_TO_EDGE" => 33071,
        "MIRRORED_REPEAT" => 33648,
        _ => throw new InvalidDataException($"Unsupported glTF texture wrap mode '{mode}'.")
    };

    private static MaterialInfo ReadMaterial(
        ModelRoot model,
        int materialIndex,
        IReadOnlyDictionary<int, int> textureIndexMap,
        string meshName)
    {
        if (materialIndex < 0 || materialIndex >= model.LogicalMaterials.Count)
            return MaterialInfo.Untextured;

        var material = model.LogicalMaterials[materialIndex];
        if (material.Alpha.ToString() != "OPAQUE")
            throw new InvalidDataException($"Player GLB material '{material.Name}' on mesh '{meshName}' uses unsupported alpha mode '{material.Alpha}'.");

        var baseColor = material.FindChannel("BaseColor");
        if (!baseColor.HasValue)
            return MaterialInfo.Untextured;

        var channel = baseColor.Value;
        var rgba = channel.Color;
        var textureIndex = -1;
        var uvTransform = Matrix3x2.Identity;
        if (channel.Texture is not null)
        {
            if (channel.TextureCoordinate != 0)
                throw new InvalidDataException($"Player GLB material '{material.Name}' on mesh '{meshName}' uses TEXCOORD_{channel.TextureCoordinate}; only TEXCOORD_0 is supported.");
            var sourceTextureIndex = channel.Texture.LogicalIndex;
            if (!textureIndexMap.TryGetValue(sourceTextureIndex, out textureIndex))
                throw new InvalidDataException($"Player GLB material '{material.Name}' on mesh '{meshName}' references an invalid base-color texture.");
            uvTransform = channel.TextureTransform?.Matrix ?? Matrix3x2.Identity;
        }

        return new MaterialInfo(
            new Vector3Data { X = rgba.X, Y = rgba.Y, Z = rgba.Z },
            textureIndex,
            uvTransform);
    }

    private sealed record MaterialInfo(Vector3Data DiffuseColor, int TextureIndex, Matrix3x2 UvTransform)
    {
        public static MaterialInfo Untextured { get; } = new(
            new Vector3Data { X = 1f, Y = 1f, Z = 1f },
            -1,
            Matrix3x2.Identity);
    }

    private static PlayerAnimationData ImportAnimation(
        Animation animation,
        IReadOnlyList<Node> joints,
        IReadOnlyList<PlayerBoneData> skeletonBones,
        string expectedAssetName)
    {
        var metadata = GetMetadata(animation)
            ?? throw new InvalidDataException($"Player GLB animation '{animation.Name}' is missing Super Cricket metadata.");
        if (ReadInt(metadata, "version") != MetadataVersion ||
            ReadString(metadata, "animationName") != animation.Name ||
            ReadString(metadata, "assetName") != expectedAssetName ||
            ReadString(metadata, "coordinateSystem") != "right-handed-y-up-metres")
            throw new InvalidDataException($"Player GLB animation '{animation.Name}' has mismatched Super Cricket metadata.");

        var duration = GetGameplayDuration(metadata, animation.Duration);
        if (!float.IsFinite(duration) || duration <= 0f)
            throw new InvalidDataException($"Player GLB animation '{animation.Name}' has no positive duration.");
        var sampleCount = Math.Max(1, (int)MathF.Ceiling(duration * SampleRate));
        var samples = new List<PlayerPoseSampleData>(sampleCount + 1);
        var rootIndex = Enumerable.Range(0, skeletonBones.Count)
            .FirstOrDefault(index => skeletonBones[index].ParentIndex < 0, -1);
        if (rootIndex < 0)
            throw new InvalidDataException($"Player GLB animation '{animation.Name}' has no skeleton root.");
        for (var sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
        {
            var time = MathF.Min(sampleIndex / SampleRate, duration);
            var poseBones = new List<TransformData>(joints.Count);
            for (var jointIndex = 0; jointIndex < joints.Count; jointIndex++)
            {
                var joint = joints[jointIndex];
                var localTransform = joint.GetLocalTransform(animation, time);
                if (!localTransform.IsLosslessDecomposable)
                    throw new InvalidDataException($"Player GLB joint '{joint.Name}' has an unsupported matrix transform in animation '{animation.Name}'.");
                poseBones.Add(ToTransform(localTransform.Matrix, $"'{animation.Name}' local pose for '{joint.Name}'"));
            }

            var rootMotion = SampleRootMotion(metadata, time);
            var rootTranslation = poseBones[rootIndex].Translation.ToVector3() - rootMotion.ToVector3();
            poseBones[rootIndex].Translation = Vector3Data.From(rootTranslation);

            samples.Add(new PlayerPoseSampleData
            {
                TimeSeconds = time,
                RootMotion = rootMotion,
                Bones = poseBones
            });
        }

        var events = new List<PlayerAnimationEventData>();
        var eventNodes = metadata["events"]?.AsArray()
            ?? throw new InvalidDataException($"Player GLB animation '{animation.Name}' has no event list.");
        foreach (var eventNode in eventNodes)
        {
            var eventObject = eventNode?.AsObject()
                ?? throw new InvalidDataException($"Player GLB animation '{animation.Name}' has an invalid event.");
            events.Add(new PlayerAnimationEventData
            {
                Name = ReadString(eventObject, "name"),
                TimeSeconds = ReadFloat(eventObject, "timeSeconds")
            });
        }

        return new PlayerAnimationData
        {
            Name = animation.Name,
            DurationSeconds = duration,
            Events = events,
            Samples = samples
        };
    }

    private static Vector3Data SampleRootMotion(JsonObject metadata, float time)
    {
        var rootMotion = metadata["rootMotion"]?.AsObject()
            ?? throw new InvalidDataException("Player GLB animation metadata is missing root motion.");
        var mode = ReadString(rootMotion, "mode");
        if (mode == "zero")
        {
            if (rootMotion["samples"]?.AsArray().Count > 0)
                throw new InvalidDataException("Zero player root motion must not contain samples.");
            return new Vector3Data();
        }
        if (mode != "samples")
            throw new InvalidDataException($"Unsupported player root-motion mode '{mode}'.");

        var samples = rootMotion["samples"]?.AsArray()
            ?? throw new InvalidDataException("Sampled player root motion is missing its samples.");
        if (samples.Count == 0)
            throw new InvalidDataException("Sampled player root motion must contain samples.");
        var previous = samples[0]?.AsObject()
            ?? throw new InvalidDataException("Player root-motion sample is invalid.");
        var previousTime = ReadFloat(previous, "timeSeconds");
        var previousPosition = ReadPosition(previous);
        if (time <= previousTime)
            return ToVector3Data(previousPosition);

        for (var index = 1; index < samples.Count; index++)
        {
            var next = samples[index]?.AsObject()
                ?? throw new InvalidDataException("Player root-motion sample is invalid.");
            var nextTime = ReadFloat(next, "timeSeconds");
            var nextPosition = ReadPosition(next);
            if (time <= nextTime)
            {
                var span = nextTime - previousTime;
                var amount = span > 0f ? (time - previousTime) / span : 0f;
                return ToVector3Data(Vector3.Lerp(previousPosition, nextPosition, Math.Clamp(amount, 0f, 1f)));
            }
            previous = next;
            previousTime = nextTime;
            previousPosition = nextPosition;
        }

        return ToVector3Data(previousPosition);
    }

    private static float GetGameplayDuration(JsonObject metadata, float fallbackDuration)
    {
        var rootMotion = metadata["rootMotion"]?.AsObject();
        if (rootMotion is null || ReadString(rootMotion, "mode") != "samples")
            return fallbackDuration;
        var samples = rootMotion["samples"]?.AsArray();
        if (samples is null || samples.Count == 0)
            return fallbackDuration;
        var sourceDuration = -1f;
        foreach (var sampleNode in samples)
        {
            var sample = sampleNode?.AsObject()
                ?? throw new InvalidDataException("Player root-motion sample is invalid.");
            var time = ReadFloat(sample, "timeSeconds");
            if (time <= sourceDuration)
                throw new InvalidDataException("Player root-motion sample times must increase.");
            _ = ReadPosition(sample);
            sourceDuration = time;
        }
        if (sourceDuration <= 0f || sourceDuration > fallbackDuration + 0.0001f)
            throw new InvalidDataException("Embedded player animation duration does not fit inside the GLB clip.");
        return sourceDuration;
    }

    private static Vector3 ReadPosition(JsonObject sample)
    {
        var values = sample["positionMeters"]?.AsArray()
            ?? throw new InvalidDataException("Player root-motion sample has no position.");
        if (values.Count != 3)
            throw new InvalidDataException("Player root-motion sample positions must contain XYZ values.");
        return new Vector3(ReadFloat(values[0]), ReadFloat(values[1]), ReadFloat(values[2]));
    }

    private static JsonObject? GetMetadata(Animation animation) =>
        animation.Extras?["superCricket"] as JsonObject;

    private static string ReadString(JsonObject value, string property) =>
        value[property]?.GetValue<string>()
        ?? throw new InvalidDataException($"Player GLB metadata is missing string '{property}'.");

    private static int ReadInt(JsonObject value, string property) =>
        value[property]?.GetValue<int>()
        ?? throw new InvalidDataException($"Player GLB metadata is missing integer '{property}'.");

    private static float ReadFloat(JsonObject value, string property) => ReadFloat(value[property]);

    private static float ReadFloat(JsonNode? value)
    {
        if (value is null || !double.TryParse(value.ToJsonString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
            throw new InvalidDataException("Player GLB metadata contains a missing or non-finite number.");
        var converted = (float)result;
        if (!float.IsFinite(converted))
            throw new InvalidDataException("Player GLB metadata number exceeds the supported range.");
        return converted;
    }

    private static TransformData ToTransform(Matrix4x4 matrix, string context)
    {
        if (!Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation) ||
            !IsFinite(scale) || !IsFinite(rotation) || !IsFinite(translation))
            throw new InvalidDataException($"Player GLB has a non-decomposable transform in {context}: {matrix}.");
        return new TransformData
        {
            Translation = Vector3Data.From(translation),
            Rotation = QuaternionData.From(Quaternion.Normalize(rotation)),
            Scale = Vector3Data.From(scale)
        };
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsIdentity(Matrix4x4 matrix) =>
        MathF.Abs(matrix.M11 - 1f) < 1e-5f && MathF.Abs(matrix.M22 - 1f) < 1e-5f &&
        MathF.Abs(matrix.M33 - 1f) < 1e-5f && MathF.Abs(matrix.M44 - 1f) < 1e-5f &&
        MathF.Abs(matrix.M12) < 1e-5f && MathF.Abs(matrix.M13) < 1e-5f && MathF.Abs(matrix.M14) < 1e-5f &&
        MathF.Abs(matrix.M21) < 1e-5f && MathF.Abs(matrix.M23) < 1e-5f && MathF.Abs(matrix.M24) < 1e-5f &&
        MathF.Abs(matrix.M31) < 1e-5f && MathF.Abs(matrix.M32) < 1e-5f && MathF.Abs(matrix.M34) < 1e-5f &&
        MathF.Abs(matrix.M41) < 1e-5f && MathF.Abs(matrix.M42) < 1e-5f && MathF.Abs(matrix.M43) < 1e-5f;

    private static bool IsFinite(Quaternion value) => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);

    private static Vector3Data ToVector3Data(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };

    private static void WriteVector3(float[] destination, int offset, Vector3 value)
    {
        destination[offset] = value.X;
        destination[offset + 1] = value.Y;
        destination[offset + 2] = value.Z;
    }

    private static void WriteVector4(float[] destination, int offset, Vector4 value)
    {
        destination[offset] = value.X;
        destination[offset + 1] = value.Y;
        destination[offset + 2] = value.Z;
        destination[offset + 3] = value.W;
    }

    private static void WriteJointIndices(
        int[] destination,
        int offset,
        Vector4 joints,
        IReadOnlyList<int> originalToOrdered,
        string meshName,
        int vertexIndex)
    {
        for (var influence = 0; influence < 4; influence++)
        {
            var value = influence switch
            {
                0 => joints.X,
                1 => joints.Y,
                2 => joints.Z,
                _ => joints.W
            };
            var originalIndex = (int)MathF.Round(value);
            if (MathF.Abs(value - originalIndex) > 0.001f || originalIndex < 0 || originalIndex >= originalToOrdered.Count)
                throw new InvalidDataException($"Player GLB mesh '{meshName}' vertex {vertexIndex} references an invalid joint index.");
            destination[offset + influence] = originalToOrdered[originalIndex];
        }
    }
}
