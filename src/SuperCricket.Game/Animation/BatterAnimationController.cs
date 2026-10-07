using System;
using SuperCricket.Content;

namespace SuperCricket.Game.Animation;

/// <summary>Independent batter poses with shared running transitions; no graphics device is required.</summary>
internal sealed class BatterAnimationController
{
    private const float TransitionSeconds = 0.12f;
    private bool _running;

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
        Striker.Play("practice-stance", TransitionSeconds);
        NonStriker.Play("practice-stance", TransitionSeconds);
    }

    public void SetRunning(bool running)
    {
        if (_running == running)
            return;
        _running = running;
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
    }
}
