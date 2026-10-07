using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SuperCricket.Content;

namespace SuperCricket.Content.Tests;

public sealed class PlayerGlbTextureTests
{
    private const uint JsonChunkType = 0x4E4F534A;
    private const uint BinaryChunkType = 0x004E4942;
    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";
    private const string JpegBase64 = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAACAAIDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAb/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFAEBAAAAAAAAAAAAAAAAAAAABv/EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAMAwEAAhEDEQA/AIQAxFX/2Q==";

    [Fact]
    public void Load_UntexturedGlbKeepsMeshesOnWhiteFallback()
    {
        var asset = PlayerAsset.Load(GetFixturePath());

        Assert.Empty(asset.Textures);
        Assert.NotEmpty(asset.Meshes);
        Assert.All(asset.Meshes, mesh => Assert.Equal(-1, mesh.BaseColorTextureIndex));
    }

    [Theory]
    [InlineData("image/png", PngBase64)]
    [InlineData("image/jpeg", JpegBase64)]
    public void Load_ImportsEmbeddedBaseColorTextureAndSampler(string mimeType, string base64)
    {
        var image = Convert.FromBase64String(base64);
        var path = CreateTexturedFixture(0, image, mimeType);
        try
        {
            var asset = PlayerAsset.Load(path);

            var texture = Assert.Single(asset.Textures);
            Assert.Equal(mimeType, texture.MimeType);
            Assert.Equal(image, texture.Content);
            Assert.Equal(9985, texture.MinFilter);
            Assert.Equal(9728, texture.MagFilter);
            Assert.Equal(33648, texture.WrapU);
            Assert.Equal(33071, texture.WrapV);
            Assert.Contains(asset.Meshes, mesh => mesh.BaseColorTextureIndex == 0);
            Assert.Empty(asset.Validate());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_RejectsUnsupportedTextureCoordinateSet()
    {
        var path = CreateTexturedFixture(1, Convert.FromBase64String(PngBase64), "image/png");
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => PlayerAsset.Load(path));
            Assert.Contains("TEXCOORD_1", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_RejectsTransparentBaseColorMaterial()
    {
        var path = CreateTexturedFixture(0, Convert.FromBase64String(PngBase64), "image/png", alphaMode: "BLEND");
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => PlayerAsset.Load(path));
            Assert.Contains("unsupported alpha mode 'BLEND'", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_AppliesGltfTextureTransformToUvCoordinates()
    {
        var original = PlayerAsset.Load(GetFixturePath());
        var path = CreateTexturedFixture(0, Convert.FromBase64String(PngBase64), "image/png", includeTransform: true);
        try
        {
            var textured = PlayerAsset.Load(path);
            var meshIndex = textured.Meshes.FindIndex(mesh => mesh.BaseColorTextureIndex == 0);
            Assert.True(meshIndex >= 0);

            var sourceUvs = original.Meshes[meshIndex].TextureCoordinates;
            var transformedUvs = textured.Meshes[meshIndex].TextureCoordinates;
            var transform = Matrix3x2.CreateScale(0.5f, 0.75f)
                * Matrix3x2.CreateRotation(-0.25f)
                * Matrix3x2.CreateTranslation(0.2f, 0.1f);
            for (var index = 0; index < sourceUvs.Length; index += 2)
            {
                var expected = Vector2.Transform(new Vector2(sourceUvs[index], sourceUvs[index + 1]), transform);
                Assert.Equal(expected.X, transformedUvs[index], precision: 5);
                Assert.Equal(expected.Y, transformedUvs[index + 1], precision: 5);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string GetFixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "practice-batter-humanoid.glb");

    private static string CreateTexturedFixture(
        int textureCoordinate,
        byte[] imageData,
        string mimeType,
        bool includeTransform = false,
        string? alphaMode = null)
    {
        var source = File.ReadAllBytes(GetFixturePath());
        var offset = 12;
        var json = ReadChunk(source, ref offset, JsonChunkType);
        var binary = ReadChunk(source, ref offset, BinaryChunkType);
        var document = JsonNode.Parse(json)
            ?? throw new InvalidDataException("The GLB fixture has no JSON document.");

        var primitive = document["meshes"]![0]!["primitives"]![0]!;
        var materialIndex = primitive["material"]!.GetValue<int>();
        var material = document["materials"]![materialIndex]!;
        var pbr = material["pbrMetallicRoughness"] as JsonObject
            ?? throw new InvalidDataException("The GLB fixture material has no PBR parameters.");
        if (alphaMode is not null)
            material["alphaMode"] = alphaMode;

        var imageOffset = Align4(binary.Length);
        var imageViewIndex = document["bufferViews"]!.AsArray().Count;
        document["bufferViews"]!.AsArray().Add(new JsonObject
        {
            ["buffer"] = 0,
            ["byteOffset"] = imageOffset,
            ["byteLength"] = imageData.Length
        });
        document["images"] = new JsonArray(new JsonObject
        {
            ["name"] = "test-red",
            ["bufferView"] = imageViewIndex,
            ["mimeType"] = mimeType
        });
        document["samplers"] = new JsonArray(new JsonObject
        {
            ["minFilter"] = 9985,
            ["magFilter"] = 9728,
            ["wrapS"] = 33648,
            ["wrapT"] = 33071
        });
        document["textures"] = new JsonArray(new JsonObject
        {
            ["sampler"] = 0,
            ["source"] = 0
        });
        var baseColorTexture = new JsonObject
        {
            ["index"] = 0,
            ["texCoord"] = textureCoordinate
        };
        if (includeTransform)
        {
            document["extensionsUsed"] = new JsonArray("KHR_texture_transform");
            baseColorTexture["extensions"] = new JsonObject
            {
                ["KHR_texture_transform"] = new JsonObject
                {
                    ["offset"] = new JsonArray(0.2f, 0.1f),
                    ["scale"] = new JsonArray(0.5f, 0.75f),
                    ["rotation"] = 0.25f
                }
            };
        }
        pbr["baseColorTexture"] = baseColorTexture;
        document["buffers"]![0]!["byteLength"] = imageOffset + imageData.Length;

        var binaryWithImage = new byte[Align4(imageOffset + imageData.Length)];
        Buffer.BlockCopy(binary, 0, binaryWithImage, 0, binary.Length);
        Buffer.BlockCopy(imageData, 0, binaryWithImage, imageOffset, imageData.Length);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document);
        var jsonPadded = new byte[Align4(jsonBytes.Length)];
        Buffer.BlockCopy(jsonBytes, 0, jsonPadded, 0, jsonBytes.Length);
        Array.Fill(jsonPadded, (byte)' ', jsonBytes.Length, jsonPadded.Length - jsonBytes.Length);

        var path = Path.Combine(Path.GetTempPath(), $"super-cricket-texture-{Guid.NewGuid():N}.glb");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("glTF"u8);
        writer.Write(2u);
        writer.Write((uint)(12 + 8 + jsonPadded.Length + 8 + binaryWithImage.Length));
        WriteChunk(writer, jsonPadded, JsonChunkType);
        WriteChunk(writer, binaryWithImage, BinaryChunkType);
        return path;
    }

    private static byte[] ReadChunk(byte[] source, ref int offset, uint expectedType)
    {
        var length = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset, 4));
        var type = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 4, 4));
        offset += 8;
        if (type != expectedType || length > source.Length - offset)
            throw new InvalidDataException("The GLB fixture has an invalid chunk.");
        var chunk = source.AsSpan(offset, checked((int)length)).ToArray();
        offset += checked((int)length);
        return chunk;
    }

    private static void WriteChunk(BinaryWriter writer, byte[] content, uint type)
    {
        writer.Write((uint)content.Length);
        writer.Write(type);
        writer.Write(content);
    }

    private static int Align4(int value) => (value + 3) & ~3;
}
