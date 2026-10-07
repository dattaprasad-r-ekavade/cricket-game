using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace SuperCricket.Game;

internal enum RecordedBattingStroke
{
    Defence,
    FrontFootDrive,
    BackFootDrive,
    FrontFootLoft,
    BackFootLoft
}

internal readonly record struct BattingInputCommand(
    RecordedBattingStroke Stroke,
    string ControlLabel,
    string AnimationClip)
{
    public string ShotName => Stroke switch
    {
        RecordedBattingStroke.Defence => "defence",
        RecordedBattingStroke.FrontFootLoft or RecordedBattingStroke.BackFootLoft => "loft",
        _ => "drive"
    };
}

/// <summary>Records a short, ordered keyboard chord so slightly staggered key presses select one stroke.</summary>
internal sealed class BattingInputRecorder
{
    private const float ChordWindowSeconds = 0.22f;
    private static readonly HashSet<Keys> RecordedKeys = [Keys.S, Keys.W, Keys.D, Keys.LeftShift, Keys.RightShift];
    private readonly List<Keys> _sequence = [];
    private readonly List<Keys[]> _keyBatches = [];
    private float _elapsed;
    private BattingInputCommand? _lastCommand;

    public bool HasPendingInput => _sequence.Count > 0;
    public string PendingLabel => string.Join(" > ", _keyBatches.Select(batch =>
        string.Join(" + ", batch.Select(FormatKey))));

    public BattingInputCommand? Update(float elapsedSeconds, IEnumerable<Keys> newlyPressedKeys)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        ArgumentNullException.ThrowIfNull(newlyPressedKeys);

        var keys = newlyPressedKeys.Where(RecordedKeys.Contains).Distinct().ToArray();
        if (_sequence.Count == 0)
        {
            if (keys.Length == 0)
                return null;
            AppendBatch(keys);
        }
        else
        {
            _elapsed += elapsedSeconds;
            if (_elapsed < ChordWindowSeconds)
                AppendBatch(keys.Where(candidate => !_sequence.Contains(candidate)));
        }

        var command = ResolveCurrentStroke();
        var changed = command is not null && command != _lastCommand;
        if (command is not null)
            _lastCommand = command;

        if (_elapsed >= ChordWindowSeconds)
            Clear();

        return changed ? command : null;
    }

    public void Reset() => Clear();

    private BattingInputCommand? ResolveCurrentStroke()
    {
        var hasFrontFoot = _sequence.Contains(Keys.S);
        var hasBackFoot = _sequence.Contains(Keys.W);
        var hasDrive = _sequence.Contains(Keys.D);
        var lofted = _sequence.Contains(Keys.LeftShift) || _sequence.Contains(Keys.RightShift);

        if (!hasFrontFoot && !hasBackFoot)
            return null;

        // If both foot keys are entered, the most recent one is the selected foot.
        var backFoot = hasBackFoot && (!hasFrontFoot || _sequence.LastIndexOf(Keys.W) > _sequence.LastIndexOf(Keys.S));
        if (hasDrive)
        {
            if (backFoot)
                return lofted
                    ? new(RecordedBattingStroke.BackFootLoft, PendingLabel, "back-foot-loft")
                    : new(RecordedBattingStroke.BackFootDrive, PendingLabel, "back-foot-drive");
            return lofted
                ? new(RecordedBattingStroke.FrontFootLoft, PendingLabel, "lofted-drive")
                : new(RecordedBattingStroke.FrontFootDrive, PendingLabel, "front-foot-drive");
        }

        if (backFoot)
            return null;
        return lofted
            ? new(RecordedBattingStroke.FrontFootLoft, PendingLabel, "lofted-drive")
            : new(RecordedBattingStroke.Defence, PendingLabel, "defensive-block");
    }

    private void Clear()
    {
        _sequence.Clear();
        _keyBatches.Clear();
        _elapsed = 0f;
        _lastCommand = null;
    }

    private void AppendBatch(IEnumerable<Keys> keys)
    {
        var batch = keys.OrderBy(KeyOrder).ToArray();
        if (batch.Length == 0)
            return;
        _keyBatches.Add(batch);
        _sequence.AddRange(batch);
    }

    private static int KeyOrder(Keys key) => key switch
    {
        Keys.LeftShift or Keys.RightShift => 0,
        Keys.S => 1,
        Keys.W => 2,
        Keys.D => 3,
        _ => 4
    };

    private static string FormatKey(Keys key) => key switch
    {
        Keys.LeftShift or Keys.RightShift => "SHIFT",
        _ => key.ToString().ToUpperInvariant()
    };
}
