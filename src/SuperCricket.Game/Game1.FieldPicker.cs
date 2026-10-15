using System;
using System.Collections.Generic;
using System.Linq;
using SuperCricket.Content;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    private bool IsFieldingPowerplayActive
    {
        get
        {
            var powerplayOvers = Math.Max(1, (int)MathF.Ceiling(_match.OversPerInnings * 0.3f));
            return _match.LegalBalls < powerplayOvers * OverScoreboard.BallsPerOver;
        }
    }

    private IReadOnlyList<FieldFormationPreset> GetFieldPickerOptions() =>
        _fieldFormations.ForStyle(_match.CurrentBowler.BowlingAttributes.Style);

    private void OpenFieldPickerForPreview()
    {
        var options = GetFieldPickerOptions();
        var activeIntent = _activeFieldFormation?.Intent ?? FieldingIntent.Standard;
        var selected = Array.FindIndex(options.ToArray(), option => option.Intent == activeIntent);
        _fieldPickerSelectionIndex = selected >= 0 ? selected : 0;
        _previewFieldFormation = options[_fieldPickerSelectionIndex];
        _fieldPickerOpen = true;
    }

    private bool HandleFieldPickerInput(FieldPickerInputFrame input)
    {
        if (!HasHumanBowlingControls || _humanBowlingStarted || _bowlerReleased || _match.IsInningsComplete)
        {
            _fieldPickerOpen = false;
            _previewFieldFormation = null;
            return false;
        }

        if (!_fieldPickerOpen)
        {
            if (!input.OpenRequested)
                return false;
            OpenFieldPickerForPreview();
            return true;
        }

        if (input.ClosePressed)
        {
            _fieldPickerOpen = false;
            _previewFieldFormation = null;
            return true;
        }

        var options = GetFieldPickerOptions();
        if (input.PreviousPressed || input.NextPressed)
        {
            var change = input.NextPressed && !input.PreviousPressed ? 1 : -1;
            _fieldPickerSelectionIndex = (_fieldPickerSelectionIndex + change + options.Count) % options.Count;
            _previewFieldFormation = options[_fieldPickerSelectionIndex];
            return true;
        }

        if (input.SelectPressed && _previewFieldFormation is { } selected)
        {
            var placement = _matchController.ConfigureFieldFormation(
                _fieldingSide,
                _fieldFormations,
                selected.Intent,
                _match.StrikerPlayer.Handedness,
                IsFieldingPowerplayActive);
            _activeFieldFormation = selected;
            _activeFieldingTactic = placement.Tactic;
            _fieldingSide.ResetActivity();
            _fieldPickerOpen = false;
            _previewFieldFormation = null;
            return true;
        }

        return true;
    }

    private IReadOnlyList<NumericsVector3> GetFieldRadarPositions()
    {
        var formation = _fieldPickerOpen ? _previewFieldFormation : _activeFieldFormation;
        if (formation is null)
            return _fieldingSide.Positions;
        return FieldPlacementModel.Resolve(
            _fieldFormations,
            formation.Style,
            formation.Intent,
                _match.StrikerPlayer.Handedness,
                IsFieldingPowerplayActive).StartingPositions;
    }
}
