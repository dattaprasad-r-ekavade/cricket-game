using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using SuperCricket.Content;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace SuperCricket.Game.Animation;

/// <summary>Samples two Blender-authored clips and crossfades their global bone poses.</summary>
public sealed class PlayerAnimator
{
    private readonly PlayerAsset _asset;
    private readonly XnaMatrix[] _inverseBindMatrices;
    private readonly XnaMatrix[] _skinMatrices;
    private PlayerAnimationData _currentClip;
    private PlayerAnimationData? _previousClip;
    private float _currentTime;
    private float _previousTime;
    private float _transitionElapsed;
    private float _transitionDuration;

    public PlayerAnimator(PlayerAsset asset)
    {
        _asset = asset;
        _skinMatrices = new XnaMatrix[asset.Bones.Count];
        _inverseBindMatrices = new XnaMatrix[asset.Bones.Count];
        for (var boneIndex = 0; boneIndex < asset.Bones.Count; boneIndex++)
        {
            var bindPose = asset.Bones[boneIndex].BindPose.ToNumericsMatrix();
            if (!Matrix4x4.Invert(bindPose, out var inverseBindPose))
                throw new ArgumentException($"Bone '{asset.Bones[boneIndex].Name}' has an invalid bind pose.", nameof(asset));
            _inverseBindMatrices[boneIndex] = ToXna(inverseBindPose);
        }

        _currentClip = asset.Animations[0];
    }

    public string CurrentClipName => _currentClip.Name;
    public bool IsTransitioning => _previousClip is not null;

    public void Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        _currentTime = (_currentTime + elapsedSeconds) % _currentClip.DurationSeconds;
        if (_previousClip is null)
            return;

        _previousTime = (_previousTime + elapsedSeconds) % _previousClip.DurationSeconds;
        _transitionElapsed += elapsedSeconds;
        if (_transitionElapsed >= _transitionDuration)
            _previousClip = null;
    }

    public void PlayNext(float transitionSeconds = 0.35f)
    {
        var currentIndex = _asset.Animations.IndexOf(_currentClip);
        var nextIndex = (currentIndex + 1) % _asset.Animations.Count;
        _previousClip = _currentClip;
        _previousTime = _currentTime;
        _currentClip = _asset.Animations[nextIndex];
        _currentTime = 0f;
        _transitionDuration = MathF.Max(0.001f, transitionSeconds);
        _transitionElapsed = 0f;
    }

    public XnaMatrix[] GetSkinMatrices()
    {
        var blend = _previousClip is null
            ? 1f
            : MathHelper.Clamp(_transitionElapsed / _transitionDuration, 0f, 1f);

        for (var boneIndex = 0; boneIndex < _skinMatrices.Length; boneIndex++)
        {
            var currentPose = SamplePose(_currentClip, _currentTime, boneIndex);
            if (_previousClip is not null)
            {
                var previousPose = SamplePose(_previousClip, _previousTime, boneIndex);
                currentPose = TransformData.Interpolate(previousPose, currentPose, blend);
            }

            var poseMatrix = ToXna(currentPose.ToNumericsMatrix());
            _skinMatrices[boneIndex] = _inverseBindMatrices[boneIndex] * poseMatrix;
        }

        return _skinMatrices;
    }

    private static TransformData SamplePose(PlayerAnimationData clip, float timeSeconds, int boneIndex)
    {
        var samples = clip.Samples;
        var time = timeSeconds % clip.DurationSeconds;
        for (var nextIndex = 1; nextIndex < samples.Count; nextIndex++)
        {
            var next = samples[nextIndex];
            if (time > next.TimeSeconds)
                continue;

            var previous = samples[nextIndex - 1];
            var span = next.TimeSeconds - previous.TimeSeconds;
            var amount = span <= 0f ? 0f : (time - previous.TimeSeconds) / span;
            return TransformData.Interpolate(previous.Bones[boneIndex], next.Bones[boneIndex], amount);
        }

        return samples[^1].Bones[boneIndex];
    }

    private static XnaMatrix ToXna(Matrix4x4 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);
}
