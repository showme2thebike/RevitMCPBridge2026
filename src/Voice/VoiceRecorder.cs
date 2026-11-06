using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace RevitMCPBridge.Voice
{
    /// <summary>
    /// Microphone capture for push-to-talk: default input device, 16 kHz mono 16-bit, chunks converted to
    /// float as they arrive. Stops itself at VoiceAudio.MaxClip so a forgotten mic cannot record forever.
    /// NAudio's WaveInEvent runs its own thread; callbacks here never touch WPF.
    /// </summary>
    public sealed class VoiceRecorder : IDisposable
    {
        private WaveInEvent _wave;
        private readonly List<float[]> _chunks = new List<float[]>();
        private readonly object _gate = new object();
        private int _samples;

        /// <summary>Latest chunk's RMS level (0..1), for a level indicator.</summary>
        public event Action<double> Level;
        /// <summary>Fired once when the clip reaches MaxClip and recording stops on its own.</summary>
        public event Action AutoStopped;

        public bool IsRecording { get; private set; }
        public static bool HasMicrophone => WaveInEvent.DeviceCount > 0;

        /// <summary>Name of the device the last Start() opened, for the "nothing captured" message and telemetry.</summary>
        public string DeviceName { get; private set; } = "default microphone";
        /// <summary>Loudest sample and loudest 100 ms window of the current/last clip, 0..1.</summary>
        public double Peak { get; private set; }
        public double MaxWindowRms { get; private set; }

        /// <summary>
        /// waveIn device 0 is the FIRST input Windows enumerates, not the one chosen in Settings → Sound → Input,
        /// so on a machine with a webcam mic plus a headset the plugin could listen to the wrong one
        /// (2026-11-06: ten near-silent clips in a row). Prefer the default communications endpoint matched by
        /// name; otherwise WAVE_MAPPER (-1), which winmm resolves to the system default.
        /// </summary>
        public static (int Index, string Name) PickDevice()
        {
            var names = new List<string>();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                try { names.Add(WaveInEvent.GetCapabilities(i).ProductName); } catch { names.Add(""); }
            }
            string friendly = null;
            try
            {
                using (var en = new NAudio.CoreAudioApi.MMDeviceEnumerator())
                    friendly = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.Role.Communications)?.FriendlyName;
            }
            catch { }
            var idx = VoiceAudio.MatchInputDevice(names, friendly);
            if (idx >= 0) return (idx, names[idx]);
            return (-1, string.IsNullOrWhiteSpace(friendly) ? "default microphone" : friendly);
        }

        public void Start()
        {
            lock (_gate)
            {
                if (IsRecording) return;
                _chunks.Clear(); _samples = 0; Peak = 0; MaxWindowRms = 0;
                var dev = PickDevice();
                DeviceName = dev.Name;
                _wave = new WaveInEvent { DeviceNumber = dev.Index, WaveFormat = new WaveFormat(VoiceAudio.SampleRate, 16, 1), BufferMilliseconds = 100 };
                _wave.DataAvailable += OnData;
                _wave.StartRecording();
                IsRecording = true;
            }
        }

        private void OnData(object sender, WaveInEventArgs e)
        {
            var f = VoiceAudio.Pcm16ToFloat(e.Buffer, e.BytesRecorded);
            bool full;
            lock (_gate)
            {
                if (!IsRecording) return;
                _chunks.Add(f); _samples += f.Length;
                full = _samples >= VoiceAudio.MaxSamples;
            }
            var rms = VoiceAudio.Rms(f); // one 100 ms buffer = one window
            var peak = VoiceAudio.Peak(f);
            lock (_gate) { if (rms > MaxWindowRms) MaxWindowRms = rms; if (peak > Peak) Peak = peak; }
            try { Level?.Invoke(rms); } catch { }
            if (full)
            {
                Stop();
                try { AutoStopped?.Invoke(); } catch { }
            }
        }

        /// <summary>Stops capture and returns everything recorded so far as one 16 kHz float buffer.</summary>
        public float[] Stop()
        {
            WaveInEvent w;
            lock (_gate)
            {
                if (!IsRecording) return VoiceAudio.Join(_chunks);
                IsRecording = false;
                w = _wave; _wave = null;
            }
            if (w != null)
            {
                try { w.DataAvailable -= OnData; w.StopRecording(); } catch { }
                try { w.Dispose(); } catch { }
            }
            lock (_gate) return VoiceAudio.Join(_chunks);
        }

        public void Dispose() { Stop(); }
    }
}
