using System.Buffers.Binary;

namespace SuperCricket.Simulation;

public enum CricketAudioCue
{
    BatContact,
    Boundary,
    Wicket,
    Extra
}

/// <summary>Builds deterministic PCM sound-effect drafts for the early playable slice.</summary>
public static class ProceduralCricketAudio
{
    public const int SampleRate = 22050;

    public static byte[] CreatePcmSamples(CricketAudioCue cue)
    {
        var durationSeconds = cue switch
        {
            CricketAudioCue.BatContact => 0.16f,
            CricketAudioCue.Boundary => 0.48f,
            CricketAudioCue.Wicket => 0.30f,
            CricketAudioCue.Extra => 0.18f,
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown cricket audio cue.")
        };
        var sampleCount = (int)MathF.Round(SampleRate * durationSeconds);
        var samples = new byte[sampleCount * sizeof(short)];
        var noise = new Random(20261006 + (int)cue * 7919);

        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / (float)SampleRate;
            var randomNoise = (float)(noise.NextDouble() * 2.0 - 1.0);
            var (wave, envelope) = cue switch
            {
                CricketAudioCue.BatContact => CreateBatContact(time, durationSeconds, randomNoise),
                CricketAudioCue.Boundary => CreateBoundary(time, durationSeconds),
                CricketAudioCue.Wicket => CreateWicket(time, durationSeconds, randomNoise),
                CricketAudioCue.Extra => CreateExtra(time, durationSeconds, randomNoise),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown cricket audio cue.")
            };
            var amplitude = (short)MathF.Round(Math.Clamp(wave * envelope, -0.92f, 0.92f) * short.MaxValue);
            if (index == 0 || index == sampleCount - 1)
                amplitude = 0;
            BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(index * sizeof(short), sizeof(short)), amplitude);
        }

        return samples;
    }

    private static (float Wave, float Envelope) CreateBatContact(float time, float duration, float noise)
    {
        var attack = Math.Clamp(time * 250f, 0f, 1f);
        var envelope = attack * MathF.Exp(-time * 23f) * Math.Clamp(1f - time / duration, 0f, 1f);
        var woodTone = MathF.Sin(2f * MathF.PI * (185f - 90f * time) * time);
        var clickTone = MathF.Sin(2f * MathF.PI * 720f * time);
        return (woodTone * 0.50f + clickTone * 0.18f + noise * 0.28f, envelope);
    }

    private static (float Wave, float Envelope) CreateBoundary(float time, float duration)
    {
        var frequency = time switch
        {
            < 0.14f => 440f,
            < 0.29f => 587f,
            _ => 784f
        };
        var fadeIn = Math.Clamp(time * 24f, 0f, 1f);
        var fadeOut = Math.Clamp((duration - time) * 8f, 0f, 1f);
        return (MathF.Sin(2f * MathF.PI * frequency * time), MathF.Min(fadeIn, fadeOut) * 0.42f);
    }

    private static (float Wave, float Envelope) CreateWicket(float time, float duration, float noise)
    {
        var attack = Math.Clamp(time * 250f, 0f, 1f);
        var envelope = attack * MathF.Exp(-time * 15f) * Math.Clamp(1f - time / duration, 0f, 1f);
        var lowThud = MathF.Sin(2f * MathF.PI * 92f * time);
        var timberTone = MathF.Sin(2f * MathF.PI * 248f * time);
        return (lowThud * 0.50f + timberTone * 0.22f + noise * 0.25f, envelope);
    }

    private static (float Wave, float Envelope) CreateExtra(float time, float duration, float noise)
    {
        var attack = Math.Clamp(time * 250f, 0f, 1f);
        var envelope = attack * MathF.Exp(-time * 18f) * Math.Clamp(1f - time / duration, 0f, 1f);
        var alertTone = MathF.Sin(2f * MathF.PI * 610f * time);
        return (alertTone * 0.55f + noise * 0.08f, envelope);
    }
}
