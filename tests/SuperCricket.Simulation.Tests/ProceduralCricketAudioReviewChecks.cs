using System.Buffers.Binary;

namespace SuperCricket.Simulation;

public static class ProceduralCricketAudioReviewChecks
{
    public static void Run()
    {
        var cues = Enum.GetValues<CricketAudioCue>();
        var clips = new List<byte[]>(cues.Length);
        foreach (var cue in cues)
        {
            var first = ProceduralCricketAudio.CreatePcmSamples(cue);
            var replay = ProceduralCricketAudio.CreatePcmSamples(cue);
            Require(first.Length > 0 && first.Length % sizeof(short) == 0 && first.SequenceEqual(replay),
                $"the {cue} cue did not produce stable 16-bit PCM samples");
            var durationSeconds = cue switch
            {
                CricketAudioCue.BatContact => 0.16f,
                CricketAudioCue.Boundary => 0.48f,
                CricketAudioCue.Wicket => 0.30f,
                CricketAudioCue.Extra => 0.18f,
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown cricket audio cue.")
            };
            Require(first.Length == (int)MathF.Round(ProceduralCricketAudio.SampleRate * durationSeconds) * sizeof(short),
                $"the {cue} cue did not use its expected duration");
            Require(BinaryPrimitives.ReadInt16LittleEndian(first) == 0 &&
                BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(first.Length - sizeof(short))) == 0,
                $"the {cue} cue did not fade cleanly at both clip edges");
            var peak = 0;
            for (var offset = 0; offset < first.Length; offset += sizeof(short))
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(offset, sizeof(short)));
                peak = Math.Max(peak, Math.Abs((int)sample));
            }
            Require(peak is > 1000 and <= 30146, $"the {cue} cue was silent or clipped");
            clips.Add(first);
        }

        for (var left = 0; left < clips.Count; left++)
        for (var right = left + 1; right < clips.Count; right++)
            Require(!clips[left].SequenceEqual(clips[right]), "two different cues generated identical PCM data");

        try
        {
            _ = ProceduralCricketAudio.CreatePcmSamples((CricketAudioCue)99);
            throw new InvalidOperationException("an unsupported sound cue was accepted");
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        Console.WriteLine("PASS: bat, boundary, wicket, and extra cues generate distinct, deterministic, clean-edged 16-bit PCM drafts.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Audio cue check failed: {message}");
    }
}
