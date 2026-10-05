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

    public static VertexPositionColorNormal[] CreateField(bool nearWicketBroken = false)
    {
        var mesh = new MeshBuilder();
        const float fieldWidth = 120f;
        const float fieldLength = 110f;

        mesh.Quad(
            new Vector3(-fieldWidth / 2f, -0.08f, -fieldLength / 2f),
            new Vector3(fieldWidth / 2f, -0.08f, -fieldLength / 2f),
            new Vector3(fieldWidth / 2f, -0.08f, fieldLength / 2f),
            new Vector3(-fieldWidth / 2f, -0.08f, fieldLength / 2f),
            new Color(48, 112, 60));

        // Broad mowing bands make camera scale and field orientation easier to read.
        for (var i = 0; i < 12; i++)
        {
            var left = -fieldWidth / 2f + i * 10f;
            var right = MathF.Min(left + 10f, fieldWidth / 2f);
            var color = i % 2 == 0 ? new Color(54, 123, 65) : new Color(48, 112, 60);
            mesh.Quad(
                new Vector3(left, -0.075f, -fieldLength / 2f),
                new Vector3(right, -0.075f, -fieldLength / 2f),
                new Vector3(right, -0.075f, fieldLength / 2f),
                new Vector3(left, -0.075f, fieldLength / 2f),
                color);
        }

        mesh.Quad(
            new Vector3(-PitchWidth / 2f, -0.025f, -PitchLength / 2f),
            new Vector3(PitchWidth / 2f, -0.025f, -PitchLength / 2f),
            new Vector3(PitchWidth / 2f, -0.025f, PitchLength / 2f),
            new Vector3(-PitchWidth / 2f, -0.025f, PitchLength / 2f),
            new Color(151, 116, 79));

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

    public static VertexPositionColorNormal[] CreateFielderMarker()
    {
        var mesh = new MeshBuilder();
        var shirt = new Color(218, 190, 105);
        var trousers = new Color(225, 224, 211);
        var skin = new Color(116, 75, 49);
        mesh.Cuboid(new Vector3(0f, 0.64f, 0f), new Vector3(0.28f, 0.46f, 0.17f), shirt);
        mesh.Cuboid(new Vector3(-0.075f, 0.22f, 0f), new Vector3(0.11f, 0.39f, 0.13f), trousers);
        mesh.Cuboid(new Vector3(0.075f, 0.22f, 0f), new Vector3(0.11f, 0.39f, 0.13f), trousers);
        mesh.Cuboid(new Vector3(-0.205f, 0.62f, 0f), new Vector3(0.11f, 0.40f, 0.12f), shirt);
        mesh.Cuboid(new Vector3(0.205f, 0.62f, 0f), new Vector3(0.11f, 0.40f, 0.12f), shirt);
        mesh.Cuboid(new Vector3(0f, 1.02f, 0f), new Vector3(0.18f, 0.19f, 0.18f), skin);
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
