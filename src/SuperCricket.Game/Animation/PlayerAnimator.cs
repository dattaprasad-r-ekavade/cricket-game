using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using SuperCricket.Content;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game.Animation;

/// <summary>Samples two Blender-authored clips and crossfades their global bone poses.</summary>
public sealed class PlayerAnimator
{
    private readonly PlayerAsset _asset;
    private readonly XnaMatrix[] _inverseBindMatrices;
    private readonly XnaMatrix[] _skinMatrices;
    private readonly XnaMatrix[] _poseMatrices;
    private PlayerAnimationData _currentClip;
    private PlayerAnimationData? _previousClip;
    private float _currentTime;
    private float _previousTime;
    private float _transitionElapsed;
    private float _transitionDuration;
    private bool _currentClipLoops = true;
    private bool _previousClipLoops = true;

    public PlayerAnimator(PlayerAsset asset)
    {
        _asset = asset;
        _skinMatrices = new XnaMatrix[asset.Bones.Count];
        _poseMatrices = new XnaMatrix[asset.Bones.Count];
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
    public float CurrentTimeSeconds => _currentTime;
    public bool IsOneShotComplete => !_currentClipLoops && _currentTime >= _currentClip.DurationSeconds && _previousClip is null;

    public void Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        _currentTime = _currentClipLoops
            ? (_currentTime + elapsedSeconds) % _currentClip.DurationSeconds
            : MathF.Min(_currentTime + elapsedSeconds, _currentClip.DurationSeconds);
        if (_previousClip is null)
            return;

        _previousTime = _previousClipLoops
            ? (_previousTime + elapsedSeconds) % _previousClip.DurationSeconds
            : MathF.Min(_previousTime + elapsedSeconds, _previousClip.DurationSeconds);
        _transitionElapsed += elapsedSeconds;
        if (_transitionElapsed >= _transitionDuration)
            _previousClip = null;
    }

    public void PlayNext(float transitionSeconds = 0.35f)
    {
        var currentIndex = _asset.Animations.IndexOf(_currentClip);
        var nextIndex = (currentIndex + 1) % _asset.Animations.Count;
        Play(_asset.Animations[nextIndex].Name, transitionSeconds);
    }

    public void Play(string clipName, float transitionSeconds = 0.35f)
    {
        Play(clipName, transitionSeconds, loops: true);
    }

    public void PlayOnce(string clipName, float transitionSeconds = 0.35f)
    {
        Play(clipName, transitionSeconds, loops: false);
    }

    private void Play(string clipName, float transitionSeconds, bool loops)
    {
        PlayerAnimationData? nextClip = null;
        foreach (var animation in _asset.Animations)
        {
            if (string.Equals(animation.Name, clipName, StringComparison.OrdinalIgnoreCase))
            {
                nextClip = animation;
                break;
            }
        }
        if (nextClip is null)
            throw new ArgumentException($"Player asset has no animation clip named '{clipName}'.", nameof(clipName));

        _previousClip = _currentClip;
        _previousTime = _currentTime;
        _previousClipLoops = _currentClipLoops;
        _currentClip = nextClip;
        _currentTime = 0f;
        _currentClipLoops = loops;
        _transitionDuration = MathF.Max(0.001f, transitionSeconds);
        _transitionElapsed = 0f;
    }

    public XnaMatrix[] GetSkinMatrices()
    {
        var blend = GetTransitionBlend();
        var currentSamples = FindPoseSamples(_currentClip, _currentTime);
        var previousClip = _previousClip;
        var previousSamples = previousClip is null
            ? default
            : FindPoseSamples(previousClip, _previousTime);

        for (var boneIndex = 0; boneIndex < _skinMatrices.Length; boneIndex++)
        {
            var currentPose = SamplePose(currentSamples, boneIndex);
            if (previousClip is not null)
            {
                var previousPose = SamplePose(previousSamples, boneIndex);
                currentPose = PoseTransform.Interpolate(previousPose, currentPose, blend);
            }

            var poseMatrix = ToXna(currentPose.ToNumericsMatrix());
            if (_asset.PoseSpace == "local" && _asset.Bones[boneIndex].ParentIndex >= 0)
                poseMatrix *= _poseMatrices[_asset.Bones[boneIndex].ParentIndex];
            _poseMatrices[boneIndex] = poseMatrix;
            _skinMatrices[boneIndex] = _inverseBindMatrices[boneIndex] * poseMatrix;
        }

        return _skinMatrices;
    }

    public XnaVector3 GetRootMotion()
    {
        var currentMotion = GetCurrentClipRootMotion();
        if (_previousClip is null)
            return currentMotion;

        var previousMotion = SampleRootMotion(_previousClip, _previousTime);
        return XnaVector3.Lerp(previousMotion, currentMotion, GetTransitionBlend());
    }

    public XnaVector3 GetCurrentClipRootMotion() => SampleRootMotion(_currentClip, _currentTime);

    public XnaVector3 GetRootMotionAtEnd(string clipName)
    {
        var clip = _asset.Animations.Find(animation =>
            string.Equals(animation.Name, clipName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Player asset has no animation clip named '{clipName}'.", nameof(clipName));
        var motion = clip.Samples[^1].RootMotion.ToVector3();
        return new XnaVector3(motion.X, motion.Y, motion.Z);
    }

    private float GetTransitionBlend() => _previousClip is null
        ? 1f
        : MathHelper.Clamp(_transitionElapsed / _transitionDuration, 0f, 1f);

    private static XnaVector3 SampleRootMotion(PlayerAnimationData clip, float timeSeconds)
    {
        var samples = clip.Samples;
        var time = Math.Clamp(timeSeconds, 0f, clip.DurationSeconds);
        for (var nextIndex = 1; nextIndex < samples.Count; nextIndex++)
        {
            var next = samples[nextIndex];
            if (time > next.TimeSeconds)
                continue;

            var previous = samples[nextIndex - 1];
            var span = next.TimeSeconds - previous.TimeSeconds;
            var amount = span <= 0f ? 0f : (time - previous.TimeSeconds) / span;
            var motion = System.Numerics.Vector3.Lerp(
                previous.RootMotion.ToVector3(),
                next.RootMotion.ToVector3(),
                amount);
            return new XnaVector3(motion.X, motion.Y, motion.Z);
        }

        var finalMotion = samples[^1].RootMotion.ToVector3();
        return new XnaVector3(finalMotion.X, finalMotion.Y, finalMotion.Z);
    }

    private static PoseSampleWindow FindPoseSamples(PlayerAnimationData clip, float timeSeconds)
    {
        var samples = clip.Samples;
        var time = Math.Clamp(timeSeconds, 0f, clip.DurationSeconds);
        for (var nextIndex = 1; nextIndex < samples.Count; nextIndex++)
        {
            var next = samples[nextIndex];
            if (time > next.TimeSeconds)
                continue;

            var previous = samples[nextIndex - 1];
            var span = next.TimeSeconds - previous.TimeSeconds;
            var amount = span <= 0f ? 0f : (time - previous.TimeSeconds) / span;
            return new PoseSampleWindow(previous, next, amount);
        }

        var finalSample = samples[^1];
        return new PoseSampleWindow(finalSample, finalSample, 0f);
    }

    private static PoseTransform SamplePose(PoseSampleWindow samples, int boneIndex)
    {
        var from = samples.Previous.Bones[boneIndex];
        if (ReferenceEquals(samples.Previous, samples.Next))
            return PoseTransform.From(from);

        return PoseTransform.Interpolate(
            PoseTransform.From(from),
            PoseTransform.From(samples.Next.Bones[boneIndex]),
            samples.Amount);
    }

    private readonly record struct PoseSampleWindow(
        PlayerPoseSampleData Previous,
        PlayerPoseSampleData Next,
        float Amount);

    private readonly record struct PoseTransform(NumericsVector3 Translation, NumericsQuaternion Rotation, NumericsVector3 Scale)
    {
        public static PoseTransform From(TransformData transform) => new(
            transform.Translation.ToVector3(),
            transform.Rotation.ToQuaternion(),
            transform.Scale.ToVector3());

        public static PoseTransform Interpolate(PoseTransform from, PoseTransform to, float amount) => new(
            NumericsVector3.Lerp(from.Translation, to.Translation, amount),
            NumericsQuaternion.Normalize(NumericsQuaternion.Slerp(from.Rotation, to.Rotation, amount)),
            NumericsVector3.Lerp(from.Scale, to.Scale, amount));

        public Matrix4x4 ToNumericsMatrix() =>
            Matrix4x4.CreateScale(Scale) *
            Matrix4x4.CreateFromQuaternion(Rotation) *
            Matrix4x4.CreateTranslation(Translation);
    }

    private static XnaMatrix ToXna(Matrix4x4 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);
}
