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

        public void Start()
        {
            lock (_gate)
            {
                if (IsRecording) return;
                _chunks.Clear(); _samples = 0;
                _wave = new WaveInEvent { WaveFormat = new WaveFormat(VoiceAudio.SampleRate, 16, 1), BufferMilliseconds = 100 };
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
            try { Level?.Invoke(VoiceAudio.Rms(f)); } catch { }
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
