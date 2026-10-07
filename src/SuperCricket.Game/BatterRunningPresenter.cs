using Microsoft.Xna.Framework;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

internal static class BatterRunningPresenter
{
    public static (Matrix Striker, Matrix NonStriker) GetWorlds(
        BetweenWicketsState runners, float nearZ, float farZ, float lateralOffsetX)
    {
        var strikerFacesFar = runners.StrikerStartsNear != runners.IsReturning;
        return (
            CreateWorld(MathHelper.Lerp(nearZ, farZ, runners.StrikerPositionFraction), strikerFacesFar, lateralOffsetX),
            CreateWorld(MathHelper.Lerp(nearZ, farZ, runners.NonStrikerPositionFraction), !strikerFacesFar));
    }

    public static Matrix CreateWorld(float z, bool facesFar, float lateralOffsetX = 0f) =>
        Matrix.CreateRotationY(facesFar ? 0f : MathHelper.Pi) *
        Matrix.CreateTranslation(new Vector3(-0.48f + lateralOffsetX, -0.025f, z));
}
