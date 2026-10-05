using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperCricket.Game.Rendering;

/// <summary>Geometry for a simple practice scene, authored in metres.</summary>
public static class PracticeGround
{
    public const float PitchLength = 20.12f;
    public const float PitchWidth = 3.05f;
    public const float WicketHeight = 0.71f;
    public const float BallRadius = 0.036f;
    public const float WicketOffset = PitchLength / 2f;

    public static readonly Vector3 BallStart = new(0f, BallRadius - 0.025f, -WicketOffset + 3f);

    public static VertexPositionColor[] CreateField()
    {
        var mesh = new MeshBuilder();
        const float fieldWidth = 110f;
        const float fieldLength = 84f;

        mesh.Quad(
            new Vector3(-fieldWidth / 2f, -0.08f, -fieldLength / 2f),
            new Vector3(fieldWidth / 2f, -0.08f, -fieldLength / 2f),
            new Vector3(fieldWidth / 2f, -0.08f, fieldLength / 2f),
            new Vector3(-fieldWidth / 2f, -0.08f, fieldLength / 2f),
            new Color(48, 112, 60));

        // Broad mowing bands make camera scale and field orientation easier to read.
        for (var i = 0; i < 11; i++)
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
        AddWicket(mesh, -WicketOffset);
        AddWicket(mesh, WicketOffset);
        return mesh.ToArray();
    }

    public static VertexPositionColor[] CreateBall()
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

    private static void AddWicket(MeshBuilder mesh, float z)
    {
        const float stumpWidth = 0.038f;
        const float stumpDepth = 0.022f;
        const float stumpGap = 0.022f;
        var stumpColor = new Color(225, 220, 199);

        for (var index = -1; index <= 1; index++)
        {
            var x = index * (stumpWidth + stumpGap);
            mesh.Cuboid(
                new Vector3(x, WicketHeight / 2f, z),
                new Vector3(stumpWidth, WicketHeight, stumpDepth),
                stumpColor);
        }

        foreach (var offset in new[] { -0.018f, 0.018f })
        {
            mesh.Cuboid(
                new Vector3(offset, WicketHeight + 0.012f, z),
                new Vector3(0.034f, 0.012f, stumpDepth * 1.2f),
                stumpColor);
        }
    }

    private sealed class MeshBuilder
    {
        private readonly List<VertexPositionColor> _vertices = [];

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            _vertices.Add(new VertexPositionColor(a, color));
            _vertices.Add(new VertexPositionColor(b, color));
            _vertices.Add(new VertexPositionColor(c, color));
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

        public VertexPositionColor[] ToArray() => _vertices.ToArray();
    }
}
