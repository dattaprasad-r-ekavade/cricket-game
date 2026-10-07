using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    private void BuildShadowVertices(Matrix strikerWorld, Matrix nonStrikerWorld, NumericsVector3 ballPosition)
    {
        _shadowVertices.Clear();
        var striker = strikerWorld.Translation;
        var nonStriker = nonStrikerWorld.Translation;
        PracticeGround.AppendSoftShadow(
            _shadowVertices,
            new Vector3(striker.X, -0.018f, striker.Z),
            0.52f,
            0.82f,
            74);
        PracticeGround.AppendSoftShadow(
            _shadowVertices,
            new Vector3(nonStriker.X, -0.018f, nonStriker.Z),
            0.52f,
            0.82f,
            74);
        var bowler = GetBowlerWorld().Translation;
        PracticeGround.AppendSoftShadow(
            _shadowVertices,
            new Vector3(bowler.X, -0.018f, bowler.Z),
            0.48f,
            0.82f,
            74);

        foreach (var fielderPosition in _fieldingSide.Positions)
        {
            var fielder = ToXna(fielderPosition);
            var overPitch = MathF.Abs(fielder.X) <= PracticeGround.PitchWidth / 2f &&
                MathF.Abs(fielder.Z) <= PracticeGround.WicketOffset;
            PracticeGround.AppendSoftShadow(
                _shadowVertices,
                new Vector3(fielder.X, overPitch ? -0.018f : -0.068f, fielder.Z),
                0.48f,
                0.72f,
                52);
        }

        if (_bowlerReleased)
        {
            var ballOverPitch = MathF.Abs(ballPosition.X) <= PracticeGround.PitchWidth / 2f &&
                MathF.Abs(ballPosition.Z) <= PracticeGround.WicketOffset;
            var groundHeight = ballOverPitch ? -0.025f : -0.075f;
            var ballHeight = MathF.Max(0f, ballPosition.Y - groundHeight);
            var opacity = (byte)Math.Clamp(72f / (1f + ballHeight * 0.55f), 12f, 72f);
            var ballRadiusX = Math.Clamp(0.09f + ballHeight * 0.11f, 0.09f, 1.2f);
            var ballRadiusZ = Math.Clamp(0.12f + ballHeight * 0.13f, 0.12f, 1.5f);
            var ballCenter = ToXna(ballPosition);
            ballCenter.Y = groundHeight + 0.006f;
            PracticeGround.AppendSoftShadow(_shadowVertices, ballCenter, ballRadiusX, ballRadiusZ, opacity);
        }
    }

    private void DrawShadows()
    {
        if (_shadowVertices.Count == 0)
            return;

        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = _worldEffect.View;
        _lineEffect.Projection = _worldEffect.Projection;
        var shadows = _shadowVertices.ToArray();
        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                shadows,
                0,
                shadows.Length / 3);
        }
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private void DrawTexturedSurfaces()
    {
        _surfaceEffect.World = Matrix.Identity;
        _surfaceEffect.View = _worldEffect.View;
        _surfaceEffect.Projection = _worldEffect.Projection;
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        _surfaceEffect.Texture = _outfieldTexture;
        foreach (var pass in _surfaceEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _outfieldVertices,
                0,
                _outfieldVertices.Length / 3);
        }

        _surfaceEffect.Texture = _pitchTexture;
        foreach (var pass in _surfaceEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _pitchVertices,
                0,
                _pitchVertices.Length / 3);
        }
    }

    private void DrawCrowd()
    {
        if (_crowdVertexBuffer is null || _crowdPrimitiveCount == 0)
            return;

        _crowdEffect.World = Matrix.Identity;
        _crowdEffect.View = _worldEffect.View;
        _crowdEffect.Projection = _worldEffect.Projection;
        foreach (var pass in _crowdEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(_crowdVertexBuffer);
            GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _crowdPrimitiveCount);
            GraphicsDevice.SetVertexBuffer(null);
        }
    }

    private Texture2D LoadTexture(string path)
    {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    private (Matrix Striker, Matrix NonStriker) GetBatterWorlds() =>
        BatterRunningPresenter.GetWorlds(_runners, NearBatterZ, FarBatterZ, _batterFootworkOffsetX);

    private static Matrix BatterWorld(float z, bool atNearEnd, float lateralOffsetX = 0f) =>
        BatterRunningPresenter.CreateWorld(z, atNearEnd, lateralOffsetX);

    private Matrix GetFielderWorld(int fielderIndex, NumericsVector3 ballPosition)
    {
        var position = _fieldingSide.Positions[fielderIndex];
        var isChasing = fielderIndex == _fieldingSide.ActiveChaserIndex && _battedBall && !_deliveryComplete;
        var isThrowing = fielderIndex == _fielderThrowerIndex && _fielderSequencePhase == FielderSequencePhase.Throw;
        var targetX = isThrowing ? _fielderThrowTarget.X : isChasing ? ballPosition.X : 0f;
        var targetZ = isThrowing ? _fielderThrowTarget.Z : isChasing ? ballPosition.Z : 0f;
        var yaw = MathF.Atan2(targetX - position.X, targetZ - position.Z);
        return Matrix.CreateRotationY(yaw) *
            Matrix.CreateScale(0.94f) *
            Matrix.CreateTranslation(ToXna(position));
    }

    private void StartFielderAction(int fielderIndex, string clipName, bool holdAtEnd)
    {
        _fielderActionClips[fielderIndex] = clipName;
        _fielderActionHoldAtEnd[fielderIndex] = holdAtEnd;
        _fielderAnimators[fielderIndex].PlayOnce(clipName, 0.12f);
    }

    private NumericsVector3 GetFielderThrowBallPosition()
    {
        if (_fielderSequencePhase == FielderSequencePhase.Pickup)
        {
            var pickupTime = _fielderAnimators[_fielderThrowerIndex].CurrentTimeSeconds;
            var securedAmount = Math.Clamp(
                pickupTime / MathF.Max(0.001f, _fielderPickupBallSecuredTimeSeconds),
                0f,
                1f);
            return NumericsVector3.Lerp(_fielderThrowStart, _fielderHeldBallPosition, securedAmount);
        }

        if (_fielderSequencePhase != FielderSequencePhase.Throw || !_fielderThrowBallReleased)
            return _fielderHeldBallPosition;

        if (_fielderThrowDurationSeconds <= _fielderThrowReleaseTimeSeconds)
            return _fielderThrowStart;

        var animationTime = _fielderAnimators[_fielderThrowerIndex].CurrentTimeSeconds;
        var flightDuration = MathF.Max(0.001f, _fielderThrowDurationSeconds - _fielderThrowReleaseTimeSeconds);
        var flightAmount = Math.Clamp((animationTime - _fielderThrowReleaseTimeSeconds) / flightDuration, 0f, 1f);
        var position = NumericsVector3.Lerp(_fielderHeldBallPosition, _fielderThrowTarget, flightAmount);
        position.Y += 3.6f * flightAmount * (1f - flightAmount);
        return position;
    }

    private Matrix GetBowlerWorld()
    {
        var release = _deliveryPreset.ReleasePosition;
        var facing = Matrix.CreateRotationY(MathHelper.Pi);
        var releasePosition = new Vector3(
            release.X + BowlerReleaseHandOffsetXMeters,
            -0.025f,
            release.Z + BowlerHandForwardMeters);
        var position = releasePosition;
        if (_bowlerActionStarted)
        {
            var deliveryMotion = _bowlerActionFinished
                ? _bowlerAnimator.GetRootMotionAtEnd("overarm-delivery")
                : _bowlerAnimator.GetCurrentClipRootMotion();
            position += Vector3.TransformNormal(deliveryMotion, facing);
        }
        else
        {
            var finalRootMotion = _bowlerAnimator.GetRootMotionAtEnd("bowling-run-up");
            var currentRootMotion = _bowlerRunUpElapsed >= _bowlerRunUpDurationSeconds
                ? finalRootMotion
                : _bowlerAnimator.GetRootMotion();
            var startPosition = releasePosition - Vector3.TransformNormal(finalRootMotion, facing);
            position = startPosition + Vector3.TransformNormal(currentRootMotion, facing);
        }

        return facing * Matrix.CreateTranslation(position);
    }

    private bool TryBatContact(
        NumericsVector3 previousBall,
        NumericsVector3 currentBall,
        Matrix batStartWorld,
        Matrix batEndWorld,
        float batPoseDeltaSeconds,
        out BattingContact contact)
    {
        contact = default;
        var expansion = _deliveryPreset.BallRadiusMeters + _shotSet.ContactPaddingMeters;
        if (!SweptBattingContactResolver.TryResolve(
            previousBall,
            currentBall,
            ToNumerics(batStartWorld),
            ToNumerics(batEndWorld),
            ToNumerics(_batBladeMinimum),
            ToNumerics(_batBladeMaximum),
            expansion,
            batPoseDeltaSeconds,
            out var resolved))
            return false;

        contact = new BattingContact(
            ToXna(resolved.Position),
            ToXna(resolved.SweetSpotPosition),
            new Vector2(resolved.NormalizedSweetSpotOffset.X, resolved.NormalizedSweetSpotOffset.Y),
            ToXna(resolved.BatPointVelocity),
            resolved.HitFraction);
        return true;
    }

    private Matrix GetBatWorldTransform()
    {
        var skinMatrices = _playerAnimator.GetSkinMatrices();
        return skinMatrices[_batBoneIndex] * BatterWorld(NearBatterZ, true, _batterFootworkOffsetX);
    }

    private float GetBatPoseFraction(float physicsElapsed, float accumulatorBeforeFrame, float elapsedSeconds, float flightElapsed)
    {
        if (elapsedSeconds <= 0.000001f)
            return 1f;

        var afterReleaseFraction = 1f - Math.Clamp(flightElapsed / elapsedSeconds, 0f, 1f);
        return Math.Clamp(afterReleaseFraction + (physicsElapsed - accumulatorBeforeFrame) / elapsedSeconds, 0f, 1f);
    }

    private float GetBatPoseDeltaSeconds(float physicsElapsed, float accumulatorBeforeFrame, float elapsedSeconds, float flightElapsed)
    {
        if (elapsedSeconds <= 0.000001f)
            return 0f;

        var start = GetBatPoseFraction(physicsElapsed, accumulatorBeforeFrame, elapsedSeconds, flightElapsed);
        var end = GetBatPoseFraction(physicsElapsed + _ballFlight.FixedTimeStepSeconds, accumulatorBeforeFrame, elapsedSeconds, flightElapsed);
        return MathF.Max(0f, end - start) * elapsedSeconds;
    }

    private Matrix InterpolateBatWorld(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        if (amount <= 0f)
            return _previousBatWorld;
        if (amount >= 1f)
            return _currentBatWorld;

        if (!_previousBatWorld.Decompose(out var previousScale, out var previousRotation, out var previousTranslation) ||
            !_currentBatWorld.Decompose(out var currentScale, out var currentRotation, out var currentTranslation))
            return Matrix.Lerp(_previousBatWorld, _currentBatWorld, amount);

        return Matrix.CreateScale(Vector3.Lerp(previousScale, currentScale, amount)) *
            Matrix.CreateFromQuaternion(Quaternion.Slerp(previousRotation, currentRotation, amount)) *
            Matrix.CreateTranslation(Vector3.Lerp(previousTranslation, currentTranslation, amount));
    }

    private static (Vector3 Minimum, Vector3 Maximum) FindBounds(float[] positions)
    {
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        for (var offset = 0; offset < positions.Length; offset += 3)
        {
            var point = new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }
        return (minimum, maximum);
    }

    private static string FormatPosition(Vector3 value) =>
        $"({value.X:0.00}, {value.Y:0.00}, {value.Z:0.00})";

    private static Vector3 ToXna(NumericsVector3 value) => new(value.X, value.Y, value.Z);
    private static NumericsVector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);
    private static System.Numerics.Matrix4x4 ToNumerics(Matrix value) => new(
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44);
}
