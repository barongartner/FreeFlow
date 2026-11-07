using NAudio.Wave;

namespace FreeFlow.Core;

/// <summary>
/// Short soft tones for record start/stop/error, generated on the fly. Each tone is
/// a quiet sine with a smooth attack and a long decay, so it reads as a soft "tick"
/// instead of a hard-edged beep (the old flat-envelope tones clicked on and off).
/// </summary>
public static class SoundFx
{
    private const int Rate = 44100;

    public static void RecordStart(AppConfig cfg) => Play(cfg, 660, 90, 0.045);
    public static void RecordStop(AppConfig cfg) => Play(cfg, 495, 90, 0.035);
    public static void ErrorTone(AppConfig cfg) => Play(cfg, 247, 180, 0.06);

    private static void Play(AppConfig cfg, double freq, int ms, double gain)
    {
        if (!cfg.PlaySounds) return;
        try
        {
            var outDev = new WaveOutEvent { DesiredLatency = 60 };
            outDev.Init(new ToneProvider(MakeTone(freq, ms, gain)));
            outDev.PlaybackStopped += (_, _) => outDev.Dispose();
            outDev.Play();
        }
        catch
        {
            // no output device: dictation still works, stay silent
        }
    }

    private static float[] MakeTone(double freq, int ms, double gain)
    {
        int n = Rate * ms / 1000;
        int attack = Rate * 8 / 1000;
        var buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            // raised-cosine attack, then exponential decay to ~0 by the end
            double env = i < attack
                ? 0.5 - 0.5 * Math.Cos(Math.PI * i / attack)
                : Math.Exp(-5.0 * (i - attack) / (n - attack));
            // soft octave overtone rounds the sine without making it brighter
            double s = Math.Sin(2 * Math.PI * freq * t) + 0.15 * Math.Sin(4 * Math.PI * freq * t);
            buf[i] = (float)(s * env * gain);
        }
        return buf;
    }

    private sealed class ToneProvider : ISampleProvider
    {
        private readonly float[] _data;
        private int _pos;

        public ToneProvider(float[] data) => _data = data;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(count, _data.Length - _pos);
            Array.Copy(_data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
    }
}
