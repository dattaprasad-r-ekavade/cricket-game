using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperCricket.Game.Rendering;

/// <summary>Measured pitch, outfield, and first procedural stadium presentation, authored in metres.</summary>
public static class PracticeGround
{
    public const float PitchLength = 20.12f;
    public const float PitchWidth = 3.05f;
    public const float WicketHeight = 0.71f;
    public const float BallRadius = 0.036f;
    public const float WicketOffset = PitchLength / 2f;
    public const int CrowdRows = 15;
    public const int CrowdSpectatorsPerRow = 320;
    public const int CrowdSpectatorCount = CrowdRows * CrowdSpectatorsPerRow;

    public static VertexPositionColorNormalTexture[] CreateOutfieldSurface()
    {
        const int segments = 256;
        const int bands = 8;
        const float radiusX = 42f;
        const float radiusZ = 42f;
        const float tileSizeMeters = 6f;
        const float surfaceY = -0.08f;
        var vertices = new List<VertexPositionColorNormalTexture>(segments * bands * 6 + segments * 3);
        var center = SurfaceVertex(new Vector3(0f, surfaceY, 0f), Color.White, tileSizeMeters);

        // Keep the mowing pattern broad and quiet so the generated turf texture remains readable.
        for (var band = 0; band < bands; band++)
        {
            var innerRadius = band / (float)bands;
            var outerRadius = (band + 1f) / bands;
            var tint = band % 2 == 0 ? new Color(224, 235, 222) : new Color(244, 246, 232);
            for (var segment = 0; segment < segments; segment++)
            {
                var angle0 = MathHelper.TwoPi * segment / segments;
                var angle1 = MathHelper.TwoPi * (segment + 1) / segments;
                var inner0 = SurfaceVertex(OutfieldPoint(innerRadius, angle0, radiusX, radiusZ, surfaceY), tint, tileSizeMeters);
                var inner1 = SurfaceVertex(OutfieldPoint(innerRadius, angle1, radiusX, radiusZ, surfaceY), tint, tileSizeMeters);
                var outer0 = SurfaceVertex(OutfieldPoint(outerRadius, angle0, radiusX, radiusZ, surfaceY), tint, tileSizeMeters);
                var outer1 = SurfaceVertex(OutfieldPoint(outerRadius, angle1, radiusX, radiusZ, surfaceY), tint, tileSizeMeters);

                if (band == 0)
                {
                    vertices.Add(center);
                    vertices.Add(outer0);
                    vertices.Add(outer1);
                }
                else
                {
                    vertices.Add(inner0);
                    vertices.Add(outer0);
                    vertices.Add(outer1);
                    vertices.Add(inner0);
                    vertices.Add(outer1);
                    vertices.Add(inner1);
                }
            }
        }

        return vertices.ToArray();
    }

    public static VertexPositionColorNormalTexture[] CreatePitchSurface()
    {
        const float halfWidth = PitchWidth / 2f;
        const float halfLength = PitchLength / 2f;
        const float tileSizeMeters = 4f;
        const float surfaceY = -0.025f;
        var tint = new Color(231, 218, 192);
        var a = SurfaceVertex(new Vector3(-halfWidth, surfaceY, -halfLength), tint, tileSizeMeters);
        var b = SurfaceVertex(new Vector3(halfWidth, surfaceY, -halfLength), tint, tileSizeMeters);
        var c = SurfaceVertex(new Vector3(halfWidth, surfaceY, halfLength), tint, tileSizeMeters);
        var d = SurfaceVertex(new Vector3(-halfWidth, surfaceY, halfLength), tint, tileSizeMeters);
        return [a, b, c, a, c, d];
    }

    public static VertexPositionColorNormal[] CreateField(bool nearWicketBroken = false)
    {
        var mesh = new MeshBuilder();
        AddPitchMarkings(mesh);
        AddWicket(mesh, -WicketOffset, nearWicketBroken);
        AddWicket(mesh, WicketOffset, false);
        AddBoundaryRope(mesh);
        AddAdvertisingBoards(mesh);
        AddSeatingBowl(mesh);
        AddScoreScreen(mesh);
        AddFloodlights(mesh);
        return mesh.ToArray();
    }

    public static VertexPositionColorNormal[] CreateCrowd()
    {
        var mesh = new MeshBuilder();
        AddCrowd(mesh);
        return mesh.ToArray();
    }

    private static Vector3 OutfieldPoint(float radius, float angle, float radiusX, float radiusZ, float y) =>
        new(MathF.Cos(angle) * radius * radiusX, y, MathF.Sin(angle) * radius * radiusZ);

    private static VertexPositionColorNormalTexture SurfaceVertex(Vector3 position, Color tint, float tileSizeMeters) =>
        new(position, tint, Vector3.Up, new Vector2(position.X / tileSizeMeters, position.Z / tileSizeMeters));

    public static void AppendSoftShadow(
        List<VertexPositionColor> vertices,
        Vector3 center,
        float radiusX,
        float radiusZ,
        byte opacity,
        int segments = 32)
    {
        var centerColor = new Color(13, 18, 15, (int)opacity);
        var edgeColor = new Color(13, 18, 15, 0);
        var centerVertex = new VertexPositionColor(center, centerColor);
        for (var segment = 0; segment < segments; segment++)
        {
            var angle0 = MathHelper.TwoPi * segment / segments;
            var angle1 = MathHelper.TwoPi * (segment + 1) / segments;
            var edge0 = new VertexPositionColor(
                center + new Vector3(MathF.Cos(angle0) * radiusX, 0f, MathF.Sin(angle0) * radiusZ),
                edgeColor);
            var edge1 = new VertexPositionColor(
                center + new Vector3(MathF.Cos(angle1) * radiusX, 0f, MathF.Sin(angle1) * radiusZ),
                edgeColor);
            vertices.Add(centerVertex);
            vertices.Add(edge0);
            vertices.Add(edge1);
        }
    }

    private static void AddBoundaryRope(MeshBuilder mesh)
    {
        AddOvalBand(mesh, 42.05f, 42.05f, 0.14f, -0.035f, new Color(233, 224, 196), 256);
    }

    private static void AddAdvertisingBoards(MeshBuilder mesh)
    {
        const int segments = 256;
        const float innerRadius = 42.8f;
        const float outerRadius = 43.0f;
        var colors = new[]
        {
            new Color(24, 51, 72), new Color(191, 137, 57),
            new Color(37, 101, 81), new Color(158, 55, 50)
        };

        for (var index = 0; index < segments; index++)
        {
            var angle0 = MathHelper.TwoPi * index / segments;
            var angle1 = MathHelper.TwoPi * (index + 1) / segments;
            var color = colors[(index / 16) % colors.Length];
            mesh.Quad(
                OvalPoint(innerRadius, innerRadius, angle0, 0.05f),
                OvalPoint(outerRadius, outerRadius, angle0, 0.05f),
                OvalPoint(outerRadius, outerRadius, angle1, 0.05f),
                OvalPoint(innerRadius, innerRadius, angle1, 0.05f),
                color);
            mesh.Quad(
                OvalPoint(innerRadius, innerRadius, angle0, 1.05f),
                OvalPoint(innerRadius, innerRadius, angle1, 1.05f),
                OvalPoint(outerRadius, outerRadius, angle1, 1.05f),
                OvalPoint(outerRadius, outerRadius, angle0, 1.05f),
                color * 0.75f);
        }
    }

    private static void AddSeatingBowl(MeshBuilder mesh)
    {
        const int segments = 192;
        const int rows = 15;
        const float startX = 44.0f;
        const float startZ = 44.0f;
        const float rowWidth = 0.67f;
        const float rowRise = 0.43f;
        const float seatDepth = 0.60f;
        var palette = new[]
        {
            new Color(25, 66, 91), new Color(31, 80, 105), new Color(135, 50, 49),
            new Color(178, 128, 65), new Color(56, 101, 75), new Color(185, 185, 168)
        };

        for (var row = 0; row < rows; row++)
        {
            var innerX = startX + row * rowWidth;
            var innerZ = startZ + row * rowWidth * 0.91f;
            var outerX = innerX + rowWidth;
            var outerZ = innerZ + rowWidth * 0.91f;
            var floorY = 0.22f + row * rowRise;
            var seatY = floorY + 0.18f;

            for (var index = 0; index < segments; index++)
            {
                var angle0 = MathHelper.TwoPi * index / segments;
                var angle1 = MathHelper.TwoPi * (index + 1) / segments;
                var seatColor = palette[((index / 8) + row / 3 + ((index + row * 5) % 17 == 0 ? 2 : 0)) % palette.Length];
                mesh.Quad(
                    OvalPoint(innerX, innerZ, angle0, seatY),
                    OvalPoint(outerX, outerZ, angle0, seatY),
                    OvalPoint(outerX, outerZ, angle1, seatY),
                    OvalPoint(innerX, innerZ, angle1, seatY),
                    seatColor);

                if (row > 0)
                {
                    mesh.Quad(
                        OvalPoint(innerX, innerZ, angle0, floorY - rowRise),
                        OvalPoint(innerX, innerZ, angle1, floorY - rowRise),
                        OvalPoint(innerX, innerZ, angle1, floorY),
                        OvalPoint(innerX, innerZ, angle0, floorY),
                        new Color(39, 49, 57));
                }

                // Short colored seat backs break up the broad tier bands into readable crowd blocks.
                var backColor = palette[(Array.IndexOf(palette, seatColor) + 1) % palette.Length];
                var backInnerX = outerX - seatDepth;
                var backInnerZ = outerZ - seatDepth * 0.91f;
                mesh.Quad(
                    OvalPoint(backInnerX, backInnerZ, angle0, floorY + 0.20f),
                    OvalPoint(outerX, outerZ, angle0, floorY + 0.20f),
                    OvalPoint(outerX, outerZ, angle1, floorY + 0.20f),
                    OvalPoint(backInnerX, backInnerZ, angle1, floorY + 0.20f),
                    backColor);
            }
        }

        AddOvalBand(mesh, 54.2f, 53.1f, 6.85f, 0.9f, new Color(47, 58, 66), 256);
        AddOvalWall(mesh, 55.0f, 53.9f, 0.05f, 7.6f, new Color(35, 46, 56), 256);
        AddOvalBand(mesh, 54.5f, 53.6f, 7.5f, 0.9f, new Color(82, 94, 98), 256);
    }

    private static void AddCrowd(MeshBuilder mesh)
    {
        const float firstRowRadius = 44.0f;
        const float rowWidth = 0.67f;
        const float rowRise = 0.43f;
        const float ellipseRatio = 0.91f;
        const float torsoHalfWidth = 0.195f;
        var shirts = new[]
        {
            new Color(30, 53, 76), new Color(54, 80, 93), new Color(104, 43, 47),
            new Color(151, 111, 68), new Color(51, 72, 55), new Color(139, 143, 137),
            new Color(71, 55, 77), new Color(156, 153, 139), new Color(35, 38, 46)
        };
        var skinTones = new[]
        {
            new Color(153, 106, 79), new Color(124, 80, 59),
            new Color(94, 60, 48), new Color(175, 131, 99)
        };
        var hairColors = new[]
        {
            new Color(35, 34, 33), new Color(72, 54, 42), new Color(126, 111, 91)
        };

        for (var row = 0; row < CrowdRows; row++)
        {
            var floorY = 0.22f + row * rowRise;
            var seatY = floorY + 0.18f;
            var rowRadius = firstRowRadius + row * rowWidth + rowWidth * 0.5f;
            var radiusZ = rowRadius * ellipseRatio;

            for (var spectator = 0; spectator < CrowdSpectatorsPerRow; spectator++)
            {
                // Offset alternating rows so people sit between the backs in the row ahead.
                var stagger = row % 2 == 0 ? 0f : 0.5f;
                var angle = MathHelper.TwoPi * (spectator + stagger) / CrowdSpectatorsPerRow;
                var cosine = MathF.Cos(angle);
                var sine = MathF.Sin(angle);
                var center = new Vector3(rowRadius * cosine, seatY + 0.015f, radiusZ * sine);
                var tangent = Vector3.Normalize(new Vector3(-rowRadius * sine, 0f, radiusZ * cosine));
                var shirtIndex = PositiveModulo(row * 71 + spectator * 37 + spectator / 11, shirts.Length);
                var skinIndex = PositiveModulo(row * 3 + spectator * 13, skinTones.Length);
                var hairIndex = PositiveModulo(row * 5 + spectator * 7, hairColors.Length);
                var shirt = shirts[shirtIndex];
                var skin = skinTones[skinIndex];
                var hair = hairColors[hairIndex];

                Vector3 Point(float x, float y) => center + tangent * x + Vector3.Up * y;

                var leftBottom = Point(-torsoHalfWidth, 0.06f);
                var rightBottom = Point(torsoHalfWidth, 0.06f);
                var rightShoulder = Point(torsoHalfWidth * 0.88f, 0.39f);
                var leftShoulder = Point(-torsoHalfWidth * 0.88f, 0.39f);
                mesh.Triangle(leftBottom, rightBottom, rightShoulder, shirt);
                mesh.Triangle(leftBottom, rightShoulder, leftShoulder, shirt);

                // Small bare arms break the shirt mass into a readable seated silhouette.
                mesh.Triangle(Point(-0.17f, 0.34f), Point(-0.22f, 0.13f), Point(-0.30f, 0.105f), skin);
                mesh.Triangle(Point(0.17f, 0.34f), Point(0.30f, 0.105f), Point(0.22f, 0.13f), skin);

                // A low-poly head and hair cap are enough to read as spectators at broadcast scale.
                var headCenter = Point(0f, 0.485f);
                const int headSegments = 8;
                for (var segment = 0; segment < headSegments; segment++)
                {
                    var angle0 = MathHelper.TwoPi * segment / headSegments;
                    var angle1 = MathHelper.TwoPi * (segment + 1) / headSegments;
                    var edge0 = headCenter + tangent * (MathF.Cos(angle0) * 0.103f)
                        + Vector3.Up * (MathF.Sin(angle0) * 0.103f);
                    var edge1 = headCenter + tangent * (MathF.Cos(angle1) * 0.103f)
                        + Vector3.Up * (MathF.Sin(angle1) * 0.103f);
                    mesh.Triangle(headCenter, edge0, edge1, skin);
                }

                var hairLeft = Point(-0.09f, 0.50f);
                var hairPeak = Point(0f, 0.585f);
                var hairRight = Point(0.09f, 0.50f);
                mesh.Triangle(hairLeft, hairPeak, hairRight, hair);
                mesh.Triangle(hairLeft, hairRight, Point(0f, 0.535f), hair);
            }
        }
    }

    private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;

    private static void AddScoreScreen(MeshBuilder mesh)
    {
        // The in-world board supplies stadium scale; the live score remains in the readable HUD.
        mesh.Cuboid(new Vector3(0f, 10.0f, -47.0f), new Vector3(15.0f, 5.4f, 0.75f), new Color(24, 39, 54));
        mesh.Cuboid(new Vector3(0f, 10.2f, -46.58f), new Vector3(13.7f, 4.25f, 0.08f), new Color(18, 63, 65));
        mesh.Cuboid(new Vector3(0f, 11.55f, -46.50f), new Vector3(11.5f, 0.16f, 0.04f), new Color(238, 196, 99));
        mesh.Cuboid(new Vector3(0f, 9.98f, -46.50f), new Vector3(9.2f, 0.12f, 0.04f), new Color(190, 206, 191));
        mesh.Cuboid(new Vector3(-4.4f, 9.35f, -46.50f), new Vector3(1.1f, 0.46f, 0.04f), new Color(233, 231, 205));
        mesh.Cuboid(new Vector3(-3.1f, 9.35f, -46.50f), new Vector3(1.1f, 0.46f, 0.04f), new Color(233, 231, 205));
        mesh.Cuboid(new Vector3(3.7f, 9.35f, -46.50f), new Vector3(1.1f, 0.46f, 0.04f), new Color(233, 231, 205));
        mesh.Cuboid(new Vector3(5.0f, 9.35f, -46.50f), new Vector3(1.1f, 0.46f, 0.04f), new Color(233, 231, 205));
    }

    private static void AddFloodlights(MeshBuilder mesh)
    {
        const float towerHeight = 29f;
        for (var tower = 0; tower < 4; tower++)
        {
            var angle = MathHelper.PiOver4 + tower * MathHelper.PiOver2;
            var x = 58.5f * MathF.Cos(angle);
            var z = 53.2f * MathF.Sin(angle);
            var mastColor = new Color(118, 130, 136);
            mesh.Cuboid(new Vector3(x, towerHeight / 2f, z), new Vector3(0.58f, towerHeight, 0.58f), mastColor);
            mesh.Cuboid(new Vector3(x, towerHeight - 0.2f, z), new Vector3(5.8f, 0.46f, 0.72f), new Color(73, 87, 94));
            mesh.Cuboid(new Vector3(x, towerHeight - 0.44f, z + 0.23f), new Vector3(5.3f, 0.20f, 0.16f), new Color(246, 233, 190));
            for (var lamp = 0; lamp < 7; lamp++)
            {
                var lampX = x - 2.3f + lamp * 0.76f;
                mesh.Cuboid(new Vector3(lampX, towerHeight - 0.42f, z + 0.34f), new Vector3(0.34f, 0.14f, 0.08f), Color.White);
            }
        }
    }

    private static void AddOvalBand(MeshBuilder mesh, float radiusX, float radiusZ, float y, float width, Color color, int segments)
    {
        var innerX = radiusX - width / 2f;
        var innerZ = radiusZ - width / 2f;
        var outerX = radiusX + width / 2f;
        var outerZ = radiusZ + width / 2f;
        for (var index = 0; index < segments; index++)
        {
            var angle0 = MathHelper.TwoPi * index / segments;
            var angle1 = MathHelper.TwoPi * (index + 1) / segments;
            mesh.Quad(
                OvalPoint(innerX, innerZ, angle0, y),
                OvalPoint(outerX, outerZ, angle0, y),
                OvalPoint(outerX, outerZ, angle1, y),
                OvalPoint(innerX, innerZ, angle1, y),
                color);
        }
    }

    private static void AddOvalWall(MeshBuilder mesh, float innerX, float innerZ, float bottom, float top, Color color, int segments)
    {
        const float thickness = 0.9f;
        for (var index = 0; index < segments; index++)
        {
            var angle0 = MathHelper.TwoPi * index / segments;
            var angle1 = MathHelper.TwoPi * (index + 1) / segments;
            mesh.Quad(
                OvalPoint(innerX, innerZ, angle0, bottom),
                OvalPoint(innerX + thickness, innerZ + thickness, angle0, bottom),
                OvalPoint(innerX + thickness, innerZ + thickness, angle1, bottom),
                OvalPoint(innerX, innerZ, angle1, bottom), color);
            mesh.Quad(
                OvalPoint(innerX, innerZ, angle0, top),
                OvalPoint(innerX, innerZ, angle1, top),
                OvalPoint(innerX + thickness, innerZ + thickness, angle1, top),
                OvalPoint(innerX + thickness, innerZ + thickness, angle0, top), color * 0.75f);
        }
    }

    private static Vector3 OvalPoint(float radiusX, float radiusZ, float angle, float y) =>
        new(radiusX * MathF.Cos(angle), y, radiusZ * MathF.Sin(angle));

    public static VertexPositionColorNormal[] CreateBall()
    {
        const int latitudeSegments = 12;
        const int longitudeSegments = 16;
        var mesh = new MeshBuilder();

        for (var lat = 0; lat < latitudeSegments; lat++)
        {
            var phi0 = MathF.PI * lat / latitudeSegments;
            var phi1 = MathF.PI * (lat + 1) / latitudeSegments;

            for (var lon = 0; lon < longitudeSegments; lon++)
            {
                var theta0 = MathHelper.TwoPi * lon / longitudeSegments;
                var theta1 = MathHelper.TwoPi * (lon + 1) / longitudeSegments;
                var color = lat == latitudeSegments / 2 ? Color.Ivory : new Color(177, 34, 42);

                var p00 = SpherePoint(phi0, theta0);
                var p01 = SpherePoint(phi0, theta1);
                var p10 = SpherePoint(phi1, theta0);
                var p11 = SpherePoint(phi1, theta1);
                mesh.Triangle(p00, p10, p11, color);
                mesh.Triangle(p00, p11, p01, color);
            }
        }

        return mesh.ToArray();
    }

    private static Vector3 SpherePoint(float latitude, float longitude)
    {
        var sinLatitude = MathF.Sin(latitude);
        return new Vector3(
            sinLatitude * MathF.Cos(longitude),
            MathF.Cos(latitude),
            sinLatitude * MathF.Sin(longitude));
    }

    private static void AddPitchMarkings(MeshBuilder mesh)
    {
        var halfWidth = PitchWidth / 2f;
        var creaseHalfWidth = halfWidth + 0.305f;
        const float creaseOffset = 1.22f;
        const float lineWidth = 0.045f;
        var white = new Color(237, 234, 216);

        foreach (var end in new[] { -1f, 1f })
        {
            var wicketZ = end * WicketOffset;
            var poppingZ = wicketZ - end * creaseOffset;
            var bowlingZ = wicketZ - end * 0.10f;
            mesh.HorizontalLine(-creaseHalfWidth, creaseHalfWidth, poppingZ, lineWidth, 0.002f, white);
            mesh.HorizontalLine(-halfWidth, halfWidth, bowlingZ, lineWidth, 0.002f, white);

            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * creaseHalfWidth;
                mesh.LongitudinalLine(x, wicketZ, poppingZ, lineWidth, 0.002f, white);
            }
        }

        mesh.HorizontalLine(-halfWidth, halfWidth, 0f, 0.018f, 0.001f, new Color(188, 154, 118));
    }

    private static void AddWicket(MeshBuilder mesh, float z, bool isBroken)
    {
        const float stumpWidth = 0.038f;
        const float stumpDepth = 0.022f;
        const float stumpGap = 0.022f;
        var stumpColor = new Color(225, 220, 199);

        for (var index = -1; index <= 1; index++)
        {
            var x = index * (stumpWidth + stumpGap);
            if (isBroken && index == 0)
            {
                mesh.Cuboid(
                    new Vector3(x, 0.04f, z + 0.17f),
                    new Vector3(stumpWidth, 0.05f, 0.38f),
                    stumpColor);
                continue;
            }

            mesh.Cuboid(
                new Vector3(x, WicketHeight / 2f, z),
                new Vector3(stumpWidth, WicketHeight, stumpDepth),
                stumpColor);
        }

        foreach (var offset in new[] { -0.018f, 0.018f })
        {
            if (isBroken)
                break;
            mesh.Cuboid(
                new Vector3(offset, WicketHeight + 0.012f, z),
                new Vector3(0.034f, 0.012f, stumpDepth * 1.2f),
                stumpColor);
        }
    }

    private sealed class MeshBuilder
    {
        private readonly List<VertexPositionColorNormal> _vertices = [];

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 0.000001f)
                normal = Vector3.Up;
            else
                normal.Normalize();

            // The top faces of the ground and seating tiers should receive the daylight
            // even when their authored triangle winding faces down.
            if (normal.Y < -0.5f)
                normal = -normal;

            _vertices.Add(new VertexPositionColorNormal(a, color, normal));
            _vertices.Add(new VertexPositionColorNormal(b, color, normal));
            _vertices.Add(new VertexPositionColorNormal(c, color, normal));
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Triangle(a, b, c, color);
            Triangle(a, c, d, color);
        }

        public void HorizontalLine(float x0, float x1, float z, float width, float y, Color color)
        {
            Quad(
                new Vector3(x0, y, z - width / 2f),
                new Vector3(x1, y, z - width / 2f),
                new Vector3(x1, y, z + width / 2f),
                new Vector3(x0, y, z + width / 2f),
                color);
        }

        public void LongitudinalLine(float x, float z0, float z1, float width, float y, Color color)
        {
            Quad(
                new Vector3(x - width / 2f, y, z0),
                new Vector3(x + width / 2f, y, z0),
                new Vector3(x + width / 2f, y, z1),
                new Vector3(x - width / 2f, y, z1),
                color);
        }

        public void Cuboid(Vector3 center, Vector3 size, Color color)
        {
            var half = size / 2f;
            var x0 = center.X - half.X;
            var x1 = center.X + half.X;
            var y0 = center.Y - half.Y;
            var y1 = center.Y + half.Y;
            var z0 = center.Z - half.Z;
            var z1 = center.Z + half.Z;
            var a = new Vector3(x0, y0, z0);
            var b = new Vector3(x1, y0, z0);
            var c = new Vector3(x1, y0, z1);
            var d = new Vector3(x0, y0, z1);
            var e = new Vector3(x0, y1, z0);
            var f = new Vector3(x1, y1, z0);
            var g = new Vector3(x1, y1, z1);
            var h = new Vector3(x0, y1, z1);

            Quad(a, b, f, e, color);
            Quad(b, c, g, f, color);
            Quad(c, d, h, g, color);
            Quad(d, a, e, h, color);
            Quad(e, f, g, h, color);
            Quad(d, c, b, a, color);
        }

        public VertexPositionColorNormal[] ToArray() => _vertices.ToArray();
    }
}
