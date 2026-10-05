using System.Numerics;

namespace SuperCricket.Content;

/// <summary>JSON-friendly three-component vector for saved gameplay data.</summary>
public sealed class Vector3Data
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3 ToVector3() => new(X, Y, Z);

    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

    public static Vector3Data From(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };
}
