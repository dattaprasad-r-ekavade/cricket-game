using System;
using SuperCricket.Content;

namespace SuperCricket.Game.Animation;

/// <summary>Independent batter poses with shared running transitions; no graphics device is required.</summary>
internal sealed class BatterAnimationController
{
    private const float TransitionSeconds = 0.12f;
    private bool _running;
    private string? _shotClipName;

    public PlayerAnimator Striker { get; }
    public PlayerAnimator NonStriker { get; }

    public BatterAnimationController(PlayerAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        Striker = new PlayerAnimator(asset);
        NonStriker = new PlayerAnimator(asset);
        ResetDelivery();
    }

    public void ResetDelivery()
    {
        _running = false;
        _shotClipName = null;
        Striker.Play("practice-stance", TransitionSeconds);
        NonStriker.Play("practice-stance", TransitionSeconds);
    }

    public void PlayShot(string clipName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clipName);
        _shotClipName = clipName;
        Striker.PlayOnce(clipName, 0.06f);
    }

    public void SetRunning(bool running)
    {
        if (_running == running)
            return;
        _running = running;
        _shotClipName = null;
        var clip = running ? "between-wickets" : "practice-stance";
        Striker.Play(clip, TransitionSeconds);
        NonStriker.Play(clip, TransitionSeconds);
    }

    public void Update(float elapsedSeconds, bool frozen = false)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (frozen)
            return;
        Striker.Update(elapsedSeconds);
        NonStriker.Update(elapsedSeconds);
        if (_shotClipName is not { } shotClipName)
            return;
        if (!string.Equals(Striker.CurrentClipName, shotClipName, StringComparison.OrdinalIgnoreCase))
        {
            _shotClipName = null;
            return;
        }
        if (Striker.IsOneShotComplete)
        {
            _shotClipName = null;
            Striker.Play("practice-stance", TransitionSeconds);
        }
    }
}
