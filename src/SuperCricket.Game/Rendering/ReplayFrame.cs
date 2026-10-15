using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperCricket.Game.Rendering;

internal sealed record ReplayActorPose(
    Matrix World,
    string ClipId,
    float ClipTimeSeconds,
    Matrix[] SkinMatrices)
{
    public static ReplayActorPose Interpolate(ReplayActorPose from, ReplayActorPose to, float amount)
    {
        var skinMatrices = new Matrix[Math.Min(from.SkinMatrices.Length, to.SkinMatrices.Length)];
        for (var index = 0; index < skinMatrices.Length; index++)
            skinMatrices[index] = Matrix.Lerp(from.SkinMatrices[index], to.SkinMatrices[index], amount);

        var useTargetClip = amount >= 0.5f;
        return new ReplayActorPose(
            Matrix.Lerp(from.World, to.World, amount),
            useTargetClip ? to.ClipId : from.ClipId,
            MathHelper.Lerp(from.ClipTimeSeconds, to.ClipTimeSeconds, amount),
            skinMatrices);
    }
}

internal sealed record ReplayFrame(
    double TimestampSeconds,
    Vector3 BallPosition,
    bool BallVisible,
    Vector3 CameraPosition,
    Vector3 CameraTarget,
    float CameraFieldOfViewDegrees,
    ReplayActorPose[] Actors,
    VertexPositionColor[] Trajectory)
{
    public static ReplayFrame Interpolate(ReplayFrame from, ReplayFrame to, double amount)
    {
        var blend = (float)Math.Clamp(amount, 0d, 1d);
        var actorCount = Math.Min(from.Actors.Length, to.Actors.Length);
        var actors = new ReplayActorPose[actorCount];
        for (var index = 0; index < actorCount; index++)
            actors[index] = ReplayActorPose.Interpolate(from.Actors[index], to.Actors[index], blend);

        return new ReplayFrame(
            from.TimestampSeconds + (to.TimestampSeconds - from.TimestampSeconds) * blend,
            Vector3.Lerp(from.BallPosition, to.BallPosition, blend),
            blend < 0.5f ? from.BallVisible : to.BallVisible,
            Vector3.Lerp(from.CameraPosition, to.CameraPosition, blend),
            Vector3.Lerp(from.CameraTarget, to.CameraTarget, blend),
            MathHelper.Lerp(from.CameraFieldOfViewDegrees, to.CameraFieldOfViewDegrees, blend),
            actors,
            blend < 0.5f ? from.Trajectory : to.Trajectory);
    }
}
