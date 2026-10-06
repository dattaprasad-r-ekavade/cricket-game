using System;
using Godot;
using SuperCricket.Content;

/// <summary>Small, repeatable stadium builder for engine presentation experiments.</summary>
public static class TrialStadiumBuilder
{
    private const int OvalSegments = 144;

    public static void Build(Node3D root, DeliveryPreset preset)
    {
        var stadium = new Node3D { Name = "ProceduralCricketGround" };
        root.AddChild(stadium);

        AddMesh(stadium, "GrassOutfield", new PlaneMesh { Size = new Vector2(208f, 176f) }, CreateTurfMaterial());
        stadium.GetNode<MeshInstance3D>("GrassOutfield").Position = new Vector3(0f, -0.12f, 0f);

        AddBoundaryRope(stadium, preset.FieldBoundaryRadiusMeters);
        AddPitch(stadium, preset);
        AddStands(stadium);
        AddSightScreens(stadium);
        AddScoreboard(stadium);
        AddFloodlights(stadium);
    }

    private static void AddPitch(Node3D root, DeliveryPreset preset)
    {
        var pitchMaterial = GrassMaterial(new Color(0.49f, 0.35f, 0.23f));
        var pitch = new MeshInstance3D
        {
            Name = "PreparedPitch",
            Mesh = new PlaneMesh { Size = new Vector2(preset.PitchWidthMeters, preset.PitchLengthMeters) },
            Position = new Vector3(0f, preset.PitchSurfaceHeightMeters, 0f),
            MaterialOverride = pitchMaterial
        };
        root.AddChild(pitch);

        var wornStrip = new MeshInstance3D
        {
            Name = "PitchWearStrip",
            Mesh = new PlaneMesh { Size = new Vector2(preset.PitchWidthMeters * 0.63f, preset.PitchLengthMeters * 0.84f) },
            Position = new Vector3(0f, preset.PitchSurfaceHeightMeters + 0.004f, 0f),
            MaterialOverride = GrassMaterial(new Color(0.59f, 0.43f, 0.28f))
        };
        root.AddChild(wornStrip);

        var creaseMaterial = UnlitMaterial(new Color(0.91f, 0.87f, 0.75f));
        var wicketMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.87f, 0.76f),
            Roughness = 0.3f,
            Metallic = 0.08f
        };
        foreach (var end in new[] { -1f, 1f })
        {
            var z = end * (preset.PitchLengthMeters * 0.5f - 0.55f);
            AddBox(root, end < 0f ? "StrikerCrease" : "BowlerCrease", new Vector3(preset.PitchWidthMeters * 0.7f, 0.022f, 0.042f), new Vector3(0f, preset.PitchSurfaceHeightMeters + 0.013f, z), creaseMaterial);
            AddStumps(root, wicketMaterial, preset.PitchSurfaceHeightMeters + 0.02f, z);
        }

        var returnCreaseZ = preset.PitchLengthMeters * 0.5f - 0.45f;
        foreach (var end in new[] { -1f, 1f })
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (preset.PitchWidthMeters * 0.5f - 0.08f);
                AddBox(root, "ReturnCrease", new Vector3(0.035f, 0.02f, 1.1f), new Vector3(x, preset.PitchSurfaceHeightMeters + 0.014f, end * returnCreaseZ), creaseMaterial);
            }
        }
    }

    private static void AddStumps(Node3D root, Material material, float ground, float z)
    {
        for (var stump = -1; stump <= 1; stump++)
        {
            var post = new MeshInstance3D
            {
                Name = "WicketStump",
                Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.72f, RadialSegments = 10, Rings = 2 },
                Position = new Vector3(stump * 0.09f, ground + 0.36f, z),
                MaterialOverride = material
            };
            root.AddChild(post);
        }
        AddBox(root, "Bail", new Vector3(0.24f, 0.035f, 0.045f), new Vector3(0f, ground + 0.735f, z), material);
    }

    private static void AddBoundaryRope(Node3D root, float radius)
    {
        var rope = new MeshInstance3D
        {
            Name = "BoundaryRope",
            Mesh = new TorusMesh
            {
                InnerRadius = radius - 0.025f,
                OuterRadius = radius + 0.025f,
                Rings = 192,
                RingSegments = 8
            },
            Position = new Vector3(0f, -0.038f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.91f, 0.87f, 0.67f),
                Roughness = 0.78f
            }
        };
        root.AddChild(rope);
    }

    private static void AddStands(Node3D root)
    {
        const int rows = 10;
        const int seatCountPerRow = 176;
        const float baseRadiusX = 79f;
        const float baseRadiusZ = 68f;

        var concrete = new[]
        {
            new Color(0.19f, 0.23f, 0.25f),
            new Color(0.24f, 0.28f, 0.29f),
            new Color(0.29f, 0.31f, 0.30f),
            new Color(0.16f, 0.21f, 0.24f)
        };
        var palette = new[]
        {
            new Color(0.05f, 0.24f, 0.32f),
            new Color(0.07f, 0.39f, 0.43f),
            new Color(0.62f, 0.25f, 0.17f),
            new Color(0.78f, 0.63f, 0.33f),
            new Color(0.83f, 0.84f, 0.78f),
            new Color(0.15f, 0.26f, 0.47f)
        };

        AddMesh(root, "LowerBowlFace", CreateOvalRing(76f, 65f, 79f, 68f, 0.12f, 2.1f), ConcreteMaterial(concrete[0]));
        AddMesh(root, "ConcourseFace", CreateOvalRing(100f, 84f, 104f, 87f, 18.5f, 21.2f), ConcreteMaterial(concrete[3]));

        var seatMesh = new BoxMesh { Size = new Vector3(0.82f, 0.48f, 0.42f) };
        var seatMaterial = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            Roughness = 0.72f,
            VertexColorUseAsAlbedo = true
        };
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = seatMesh,
            InstanceCount = rows * seatCountPerRow
        };
        var colorIndex = 0;
        for (var row = 0; row < rows; row++)
        {
            var rx = baseRadiusX + row * 2.1f;
            var rz = baseRadiusZ + row * 1.65f;
            var y = 2.25f + row * 1.75f;
            var rowColour = concrete[row % concrete.Length];
            AddMesh(root, $"TerraceRow{row + 1:00}", CreateOvalRing(rx, rz, rx + 2.1f, rz + 1.65f, y, y + 0.32f), ConcreteMaterial(rowColour));

            for (var seat = 0; seat < seatCountPerRow; seat++)
            {
                var angle = (float)(Math.Tau * seat / seatCountPerRow);
                var x = (rx + 1.02f) * MathF.Cos(angle);
                var z = (rz + 0.8f) * MathF.Sin(angle);
                var yaw = Mathf.Pi * 0.5f - angle;
                var position = new Vector3(x, y + 0.58f, z);
                multiMesh.SetInstanceTransform(colorIndex, new Transform3D(new Basis(Vector3.Up, yaw), position));
                var paletteIndex = ((seat / 8) + row * 3 + (seat % 5 == 0 ? 1 : 0)) % palette.Length;
                multiMesh.SetInstanceColor(colorIndex, palette[paletteIndex]);
                colorIndex++;
            }
        }
        var seats = new MultiMeshInstance3D { Name = "ColourBlockedStadiumSeating", Multimesh = multiMesh, MaterialOverride = seatMaterial };
        root.AddChild(seats);

        var supportMaterial = ConcreteMaterial(new Color(0.28f, 0.33f, 0.34f));
        var supportMesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.7f, Height = 19f, RadialSegments = 8, Rings = 2 };
        for (var support = 0; support < 32; support++)
        {
            var angle = (float)(Math.Tau * support / 32f);
            var x = 101.5f * MathF.Cos(angle);
            var z = 85.5f * MathF.Sin(angle);
            var post = new MeshInstance3D
            {
                Name = "RoofSupport",
                Mesh = supportMesh,
                Position = new Vector3(x, 10f, z),
                MaterialOverride = supportMaterial
            };
            root.AddChild(post);
        }

        AddMesh(root, "GrandstandRoof", CreateOvalRing(93f, 79f, 108f, 92f, 21.5f, 23.4f), new StandardMaterial3D
        {
            AlbedoColor = new Color(0.17f, 0.25f, 0.27f),
            Metallic = 0.34f,
            Roughness = 0.35f
        });
        AddMesh(root, "RoofInnerRim", CreateOvalRing(92.1f, 78.1f, 93.2f, 79.2f, 20.6f, 21.6f), ConcreteMaterial(new Color(0.59f, 0.62f, 0.56f)));
    }

    private static void AddSightScreens(Node3D root)
    {
        var screenMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.83f, 0.84f, 0.76f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Roughness = 0.88f
        };
        foreach (var z in new[] { -26f, 26f })
        {
            var screen = AddBox(root, "BowlerSightScreen", new Vector3(16f, 5.6f, 0.5f), new Vector3(0f, 3.2f, z), screenMaterial);
            screen.RotationDegrees = new Vector3(0f, z < 0f ? 0f : 180f, 0f);
            AddBox(root, "SightScreenBand", new Vector3(16.1f, 0.3f, 0.57f), new Vector3(0f, 0.53f, z), ConcreteMaterial(new Color(0.19f, 0.24f, 0.25f)));
        }
    }

    private static void AddScoreboard(Node3D root)
    {
        var screenMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.025f, 0.13f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(0.02f, 0.12f, 0.11f),
            EmissionEnergyMultiplier = 1.3f,
            Roughness = 0.27f
        };
        AddBox(root, "ScoreboardHousing", new Vector3(18f, 8f, 0.95f), new Vector3(87f, 23.5f, 49f), ConcreteMaterial(new Color(0.12f, 0.18f, 0.21f)))
            .RotationDegrees = new Vector3(0f, 62f, 0f);
        AddBox(root, "ScoreboardDisplay", new Vector3(15.8f, 5.9f, 0.12f), new Vector3(86.5f, 23.5f, 48.7f), screenMaterial)
            .RotationDegrees = new Vector3(0f, 62f, 0f);
    }

    private static void AddFloodlights(Node3D root)
    {
        var towerMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.31f, 0.33f),
            Metallic = 0.72f,
            Roughness = 0.3f
        };
        var floodlightMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.86f, 0.57f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.75f, 0.37f),
            EmissionEnergyMultiplier = 3.1f,
            Roughness = 0.18f
        };

        var locations = new[] { new Vector2(-95f, -10f), new Vector2(95f, -10f), new Vector2(-95f, 10f), new Vector2(95f, 10f) };
        for (var i = 0; i < locations.Length; i++)
        {
            var location = locations[i];
            var tower = new MeshInstance3D
            {
                Name = $"FloodlightTower{i + 1}",
                Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.72f, Height = 38f, RadialSegments = 8, Rings = 2 },
                Position = new Vector3(location.X, 19f, location.Y),
                MaterialOverride = towerMaterial
            };
            root.AddChild(tower);

            var head = AddBox(root, $"FloodlightBank{i + 1}", new Vector3(11f, 1.2f, 1.4f), new Vector3(location.X, 38.5f, location.Y), floodlightMaterial);
            head.RotationDegrees = new Vector3(0f, location.X < 0f ? 90f : -90f, 0f);
            for (var lamp = -4; lamp <= 4; lamp++)
            {
                var module = AddBox(root, "FloodlightModule", new Vector3(0.72f, 0.46f, 0.12f), new Vector3(location.X + lamp * 1.1f, 38.5f, location.Y - 0.78f), floodlightMaterial);
                module.RotationDegrees = head.RotationDegrees;
            }
        }
    }

    private static Mesh CreateOvalRing(
        float innerX,
        float innerZ,
        float outerX,
        float outerZ,
        float lowerY,
        float upperY,
        int segments = OvalSegments)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (var segment = 0; segment < segments; segment++)
        {
            var angleA = (float)(Math.Tau * segment / segments);
            var angleB = (float)(Math.Tau * (segment + 1) / segments);
            var innerA = new Vector3(innerX * MathF.Cos(angleA), upperY, innerZ * MathF.Sin(angleA));
            var innerB = new Vector3(innerX * MathF.Cos(angleB), upperY, innerZ * MathF.Sin(angleB));
            var outerA = new Vector3(outerX * MathF.Cos(angleA), upperY, outerZ * MathF.Sin(angleA));
            var outerB = new Vector3(outerX * MathF.Cos(angleB), upperY, outerZ * MathF.Sin(angleB));
            AddTriangle(tool, innerA, innerB, outerB);
            AddTriangle(tool, innerA, outerB, outerA);

            if (upperY > lowerY)
            {
                var lowInnerA = new Vector3(innerX * MathF.Cos(angleA), lowerY, innerZ * MathF.Sin(angleA));
                var lowInnerB = new Vector3(innerX * MathF.Cos(angleB), lowerY, innerZ * MathF.Sin(angleB));
                AddTriangle(tool, lowInnerA, outerA, outerB);
                AddTriangle(tool, lowInnerA, outerB, lowInnerB);
            }
        }
        return tool.Commit();
    }

    private static void AddTriangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.SetNormal(Vector3.Up);
        tool.AddVertex(a);
        tool.SetNormal(Vector3.Up);
        tool.AddVertex(b);
        tool.SetNormal(Vector3.Up);
        tool.AddVertex(c);
    }

    private static MeshInstance3D AddMesh(Node3D root, string name, Mesh mesh, Material material)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material };
        root.AddChild(instance);
        return instance;
    }

    private static MeshInstance3D AddBox(Node3D root, string name, Vector3 size, Vector3 position, Material material)
    {
        var instance = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = material
        };
        root.AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D GrassMaterial(Color colour) => new()
    {
        AlbedoColor = colour,
        Roughness = 0.93f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private static StandardMaterial3D ConcreteMaterial(Color colour) => new()
    {
        AlbedoColor = colour,
        Roughness = 0.82f
    };

    private static StandardMaterial3D UnlitMaterial(Color colour) => new()
    {
        AlbedoColor = colour,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private static ShaderMaterial CreateTurfMaterial() => new()
    {
        Shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode unshaded;
                varying vec3 local_position;

                void vertex() {
                    local_position = VERTEX;
                }

                void fragment() {
                    float radius = length(local_position.xz / vec2(76.0, 65.0));
                    float mow = mod(floor(radius * 15.0), 2.0);
                    vec3 stripe_a = vec3(0.045, 0.25, 0.06);
                    vec3 stripe_b = vec3(0.055, 0.29, 0.068);
                    vec3 outer_turf = vec3(0.035, 0.18, 0.045);
                    ALBEDO = radius < 1.0 ? mix(stripe_a, stripe_b, mow) : outer_turf;
                }
                """
        }
    };
}
