using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace RevitMCPBridge.Voice
{
    /// <summary>
    /// On-device speech to text for push-to-talk (Whisper, ggml base.en Q5_1 via Whisper.net). Everything runs
    /// on the workstation CPU: no audio leaves the machine, nothing is billed. The native libraries ship in
    /// the add-in folder under runtimes\win-x64 (not next to Revit.exe), so the loader is pointed there
    /// explicitly before the first model load. Measured 2026-11-05 on an i7-1355U laptop: ~3 s for a
    /// 7-second sentence; the vocabulary prompt turned "topo solid" into "Toposolid".
    /// </summary>
    public sealed class VoiceTranscriber : IDisposable
    {
        /// <summary>Terms the generic model mishears. Whisper treats this as preceding context, not a rule list.</summary>
        public const string RevitPrompt =
            "Revit, BIM Monkey, Banana Chat, Toposolid, viewport, sheet A101, sheet A201, elevation, section, " +
            "floor plan, callout, detail, legend, schedule, level, grid, wall type, family, parameter, Revit API, " +
            "construction documents, CDs, dimension string, text note, revision cloud, title block.";

        private static int _loaderConfigured;
        private readonly object _gate = new object();
        private WhisperFactory _factory;
        private string _factoryPath;

        /// <summary>Point Whisper.net at the native DLLs that ship beside the add-in. Safe to call repeatedly.</summary>
        public static void ConfigureLoader(string addinDirectory)
        {
            if (Interlocked.Exchange(ref _loaderConfigured, 1) != 0) return;
            var native = Path.Combine(addinDirectory, "runtimes", "win-x64");
            if (Directory.Exists(native)) RuntimeOptions.LibraryPath = native;
            else Log.Warning("[Voice] native folder missing: {Path}", native);
            // CPU only: predictable on every workstation; the AVX build first, the portable build as fallback.
            RuntimeOptions.RuntimeLibraryOrder = new List<RuntimeLibrary> { RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx };
        }

        /// <summary>Loads the model once (about a second); later calls reuse it.</summary>
        public void Load(string modelPath)
        {
            lock (_gate)
            {
                if (_factory != null && _factoryPath == modelPath) return;
                _factory?.Dispose();
                _factory = WhisperFactory.FromPath(modelPath);
                _factoryPath = modelPath;
                Log.Information("[Voice] model loaded: {Path}; {Runtime}", Path.GetFileName(modelPath), WhisperFactory.GetRuntimeInfo());
            }
        }

        public bool IsLoaded { get { lock (_gate) return _factory != null; } }

        /// <summary>16 kHz mono float samples → one line of text. Empty string when nothing was recognised.</summary>
        public async Task<string> TranscribeAsync(float[] samples, CancellationToken ct = default)
        {
            WhisperFactory factory;
            lock (_gate) { factory = _factory ?? throw new InvalidOperationException("model not loaded"); }
            var threads = Math.Max(2, Math.Min(8, Environment.ProcessorCount / 2));
            using (var processor = factory.CreateBuilder()
                .WithLanguage("en")
                .WithThreads(threads)
                .WithPrompt(RevitPrompt)
                .WithNoContext()
                .Build())
            {
                var segments = new List<string>();
                await foreach (var seg in processor.ProcessAsync(samples, ct).ConfigureAwait(false))
                    segments.Add(seg.Text);
                return VoiceAudio.CleanTranscript(segments);
            }
        }

        public void Dispose()
        {
            lock (_gate) { _factory?.Dispose(); _factory = null; _factoryPath = null; }
        }
    }
}
