using System.Numerics;

namespace SuperCricket.Content;

/// <summary>Position, orientation, and scale in a rig's armature space.</summary>
public sealed class TransformData
{
    public Vector3Data Translation { get; set; } = new();
    public QuaternionData Rotation { get; set; } = new();
    public Vector3Data Scale { get; set; } = new() { X = 1f, Y = 1f, Z = 1f };

    public Matrix4x4 ToNumericsMatrix() =>
        Matrix4x4.CreateScale(Scale.ToVector3()) *
        Matrix4x4.CreateFromQuaternion(Rotation.ToQuaternion()) *
        Matrix4x4.CreateTranslation(Translation.ToVector3());

    public bool IsFinite() =>
        Translation is not null && Translation.IsFinite() &&
        Rotation is not null && Rotation.IsFinite() &&
        Scale is not null && Scale.IsFinite();

    public static TransformData Interpolate(TransformData from, TransformData to, float amount)
    {
        return new TransformData
        {
            Translation = Vector3Data.From(Vector3.Lerp(from.Translation.ToVector3(), to.Translation.ToVector3(), amount)),
            Rotation = QuaternionData.From(Quaternion.Slerp(from.Rotation.ToQuaternion(), to.Rotation.ToQuaternion(), amount)),
            Scale = Vector3Data.From(Vector3.Lerp(from.Scale.ToVector3(), to.Scale.ToVector3(), amount))
        };
    }
}

public sealed class QuaternionData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; } = 1f;

    public Quaternion ToQuaternion()
    {
        var value = new Quaternion(X, Y, Z, W);
        return value.LengthSquared() > 0.000001f ? Quaternion.Normalize(value) : Quaternion.Identity;
    }

    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(W);

    public static QuaternionData From(Quaternion value) => new()
    {
        X = value.X,
        Y = value.Y,
        Z = value.Z,
        W = value.W
    };
}
