using System;
using System.Collections.Generic;

namespace RevitMCPBridge.Voice
{
    /// <summary>
    /// Pure audio helpers for push-to-talk: the microphone delivers 16 kHz mono 16-bit PCM, the speech
    /// model wants 16 kHz mono float samples in [-1, 1]. Kept free of NAudio and Whisper so it is unit
    /// tested in UpdateTests without a sound card.
    /// </summary>
    public static class VoiceAudio
    {
        public const int SampleRate = 16000;
        /// <summary>Longest clip we transcribe. A spoken prompt is 5–30 s; anything longer is a forgotten mic.</summary>
        public static readonly TimeSpan MaxClip = TimeSpan.FromSeconds(60);
        /// <summary>Below this a click or a breath is all the mic caught; do not bother the model.</summary>
        public static readonly TimeSpan MinClip = TimeSpan.FromMilliseconds(400);

        public static int MaxSamples => (int)(MaxClip.TotalSeconds * SampleRate);

        /// <summary>Little-endian 16-bit PCM bytes → float samples. Odd trailing byte is ignored.</summary>
        public static float[] Pcm16ToFloat(byte[] pcm, int count)
        {
            if (pcm == null || count <= 1) return Array.Empty<float>();
            var n = count / 2;
            var outp = new float[n];
            for (int i = 0; i < n; i++)
            {
                short s = (short)(pcm[2 * i] | (pcm[2 * i + 1] << 8));
                outp[i] = s / 32768f;
            }
            return outp;
        }

        /// <summary>Concatenate captured chunks into one buffer, capped at MaxClip.</summary>
        public static float[] Join(IReadOnlyList<float[]> chunks)
        {
            if (chunks == null || chunks.Count == 0) return Array.Empty<float>();
            long total = 0;
            foreach (var c in chunks) total += c.Length;
            var n = (int)Math.Min(total, MaxSamples);
            var outp = new float[n];
            int pos = 0;
            foreach (var c in chunks)
            {
                if (pos >= n) break;
                var take = Math.Min(c.Length, n - pos);
                Array.Copy(c, 0, outp, pos, take);
                pos += take;
            }
            return outp;
        }

        public static TimeSpan Duration(float[] samples) =>
            TimeSpan.FromSeconds((samples?.Length ?? 0) / (double)SampleRate);

        /// <summary>Root-mean-square level, 0..1. Used to decide whether anything was said at all.</summary>
        public static double Rms(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0;
            double acc = 0;
            foreach (var s in samples) acc += (double)s * s;
            return Math.Sqrt(acc / samples.Length);
        }

        /// <summary>Largest absolute sample, 0..1.</summary>
        public static double Peak(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0;
            double p = 0;
            foreach (var s in samples) { var a = Math.Abs((double)s); if (a > p) p = a; }
            return p;
        }

        /// <summary>
        /// Highest RMS over any <paramref name="windowMs"/> window. Whole-clip RMS punishes pauses: a 10 s clip
        /// with 2 s of speech averages below the floor while its loudest window is well above it
        /// (2026-11-06: ten clips of 3–18 s rejected in a row on a machine where one had transcribed fine).
        /// </summary>
        public static double WindowedMaxRms(float[] samples, int windowMs = 100)
        {
            if (samples == null || samples.Length == 0) return 0;
            var win = Math.Max(1, SampleRate * windowMs / 1000);
            double best = 0;
            for (int start = 0; start < samples.Length; start += win)
            {
                var len = Math.Min(win, samples.Length - start);
                double acc = 0;
                for (int i = start; i < start + len; i++) acc += (double)samples[i] * samples[i];
                var r = Math.Sqrt(acc / len);
                if (r > best) best = r;
            }
            return best;
        }

        /// <summary>True when the clip is long enough and its loudest window is loud enough to contain speech.</summary>
        public static bool LooksLikeSpeech(float[] samples, double rmsFloor = 0.004)
            => Duration(samples) >= MinClip && WindowedMaxRms(samples) >= rmsFloor;

        /// <summary>
        /// Index of the waveIn device that is the user's default microphone, or -1 for WAVE_MAPPER (system
        /// default). waveIn names are the endpoint friendly name cut to 31 characters, so match by prefix either way.
        /// </summary>
        public static int MatchInputDevice(IReadOnlyList<string> waveInNames, string defaultFriendlyName)
        {
            if (waveInNames == null || string.IsNullOrWhiteSpace(defaultFriendlyName)) return -1;
            var want = defaultFriendlyName.Trim();
            for (int i = 0; i < waveInNames.Count; i++)
            {
                var n = (waveInNames[i] ?? "").Trim();
                if (n.Length < 3) continue;
                if (want.StartsWith(n, StringComparison.OrdinalIgnoreCase) || n.StartsWith(want, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>Five-step level bar for the status line while listening. Laptop-mic speech sits around 0.01–0.05 RMS.</summary>
        public static string LevelBar(double rms)
        {
            var steps = rms <= 0.001 ? 0 : rms < 0.004 ? 1 : rms < 0.012 ? 2 : rms < 0.03 ? 3 : rms < 0.08 ? 4 : 5;
            return new string('\u25AE', steps) + new string('\u25AF', 5 - steps);
        }

        /// <summary>
        /// Whisper emits text in segments with leading spaces and sometimes a bracketed non-speech marker
        /// like [BLANK_AUDIO] or (silence). Join the segments into one line the user can read and send.
        /// </summary>
        public static string CleanTranscript(IEnumerable<string> segments)
        {
            if (segments == null) return "";
            var parts = new List<string>();
            foreach (var raw in segments)
            {
                var s = (raw ?? "").Trim();
                if (s.Length == 0) continue;
                if ((s.StartsWith("[") && s.EndsWith("]")) || (s.StartsWith("(") && s.EndsWith(")"))) continue; // [BLANK_AUDIO], (music)
                parts.Add(s);
            }
            var text = string.Join(" ", parts);
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            // The vocabulary prompt ends with a period, so the first segment often starts with ", " or ". ".
            text = text.TrimStart(' ', ',', '.', ';', ':', '-');
            if (text.Length > 0 && char.IsLower(text[0])) text = char.ToUpperInvariant(text[0]) + text.Substring(1);
            return text.Trim();
        }
    }
}
