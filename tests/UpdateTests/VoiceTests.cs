using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RevitMCPBridge.Voice;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class VoiceAudioTests
    {
        [Fact]
        public void Pcm16_little_endian_maps_to_unit_floats()
        {
            // 0x7FFF = max positive, 0x8000 = min negative, 0 = silence; trailing odd byte ignored
            var pcm = new byte[] { 0xFF, 0x7F, 0x00, 0x80, 0x00, 0x00, 0x42 };
            var f = VoiceAudio.Pcm16ToFloat(pcm, pcm.Length);
            Assert.Equal(3, f.Length);
            Assert.InRange(f[0], 0.999f, 1.0f);
            Assert.Equal(-1f, f[1]);
            Assert.Equal(0f, f[2]);
            Assert.Empty(VoiceAudio.Pcm16ToFloat(null, 0));
            Assert.Empty(VoiceAudio.Pcm16ToFloat(new byte[] { 1 }, 1));
        }

        [Fact]
        public void Join_concatenates_in_order_and_caps_at_the_max_clip()
        {
            var a = new float[] { 1, 2 }; var b = new float[] { 3 };
            Assert.Equal(new float[] { 1, 2, 3 }, VoiceAudio.Join(new[] { a, b }));
            Assert.Empty(VoiceAudio.Join(new List<float[]>()));
            var big = new float[VoiceAudio.MaxSamples + 5000];
            Assert.Equal(VoiceAudio.MaxSamples, VoiceAudio.Join(new[] { big }).Length);
            Assert.Equal(60.0, VoiceAudio.Duration(new float[VoiceAudio.MaxSamples]).TotalSeconds, 3);
        }

        [Fact]
        public void Speech_detection_rejects_short_or_silent_clips()
        {
            var silence = new float[VoiceAudio.SampleRate]; // 1 s of nothing
            Assert.False(VoiceAudio.LooksLikeSpeech(silence));
            var tooShort = Enumerable.Repeat(0.3f, VoiceAudio.SampleRate / 10).ToArray(); // 100 ms, loud
            Assert.False(VoiceAudio.LooksLikeSpeech(tooShort));
            var speechLike = Enumerable.Range(0, VoiceAudio.SampleRate * 2).Select(i => (float)(0.2 * Math.Sin(i / 20.0))).ToArray();
            Assert.True(VoiceAudio.LooksLikeSpeech(speechLike));
            Assert.Equal(0, VoiceAudio.Rms(null));
        }

        [Fact]
        public void Speech_detection_judges_the_loudest_window_not_the_whole_clip()
        {
            // 10 s clip, 1 s of quiet speech (RMS ~0.0085) and 9 s of silence: whole-clip RMS ~0.0027 fails
            // the 0.004 floor, the loudest 100 ms window passes it. Seen 2026-11-06.
            var clip = new float[VoiceAudio.SampleRate * 10];
            for (int i = 0; i < VoiceAudio.SampleRate; i++) clip[i] = (float)(0.012 * Math.Sin(i / 20.0));
            Assert.True(VoiceAudio.Rms(clip) < 0.004);
            Assert.True(VoiceAudio.WindowedMaxRms(clip) > 0.004);
            Assert.True(VoiceAudio.LooksLikeSpeech(clip));
            Assert.InRange(VoiceAudio.Peak(clip), 0.0119, 0.0121);
            Assert.Equal(0, VoiceAudio.WindowedMaxRms(null));
        }

        [Fact]
        public void A_waveInOpen_failure_is_explained_as_a_Windows_permission_problem()
        {
            var t = VoiceAudio.FriendlyStartError("MemoryAllocationError calling waveInOpen", "Headset Mic");
            Assert.StartsWith("Windows would not open the microphone (Headset Mic) for Revit.", t);
            Assert.Contains("Let desktop apps access your microphone", t);
            Assert.Contains("MemoryAllocationError calling waveInOpen", t);
            Assert.Equal("Voice input could not start: boom", VoiceAudio.FriendlyStartError("boom", null));
        }

        [Fact]
        public void Default_microphone_is_matched_by_truncated_waveIn_name()
        {
            // waveIn cuts names at 31 chars; the endpoint friendly name is the full string.
            var names = new[] { "Microphone (HD Pro Webcam C920)", "Headset Microphone (Jabra Evolv", "" };
            Assert.Equal(1, VoiceAudio.MatchInputDevice(names, "Headset Microphone (Jabra Evolve2 65)"));
            Assert.Equal(0, VoiceAudio.MatchInputDevice(names, "Microphone (HD Pro Webcam C920)"));
            Assert.Equal(-1, VoiceAudio.MatchInputDevice(names, "Microphone Array (Realtek)"));
            Assert.Equal(-1, VoiceAudio.MatchInputDevice(names, null));
            Assert.Equal(-1, VoiceAudio.MatchInputDevice(null, "x"));
        }

        [Fact]
        public void Level_bar_has_five_steps_and_moves_for_quiet_speech()
        {
            Assert.Equal("\u25AF\u25AF\u25AF\u25AF\u25AF", VoiceAudio.LevelBar(0));
            Assert.Equal(5, VoiceAudio.LevelBar(0.5).Length);
            Assert.StartsWith("\u25AE\u25AE", VoiceAudio.LevelBar(0.01));
            Assert.Equal("\u25AE\u25AE\u25AE\u25AE\u25AE", VoiceAudio.LevelBar(0.2));
        }

        [Fact]
        public void Transcript_cleanup_joins_segments_and_drops_non_speech_markers()
        {
            var text = VoiceAudio.CleanTranscript(new[] { " Place the north elevation", "  on sheet A201.", "[BLANK_AUDIO]", "(silence)", "", null, " Then renumber   the viewports." });
            Assert.Equal("Place the north elevation on sheet A201. Then renumber the viewports.", text);
            // Seen 2026-11-05: the prompt's trailing period made Whisper open with ", place the north..."
            Assert.Equal("Place the north elevation on sheet A201.", VoiceAudio.CleanTranscript(new[] { ", place the north elevation on sheet A201." }));
            Assert.Equal("", VoiceAudio.CleanTranscript(null));
            Assert.Equal("", VoiceAudio.CleanTranscript(new[] { "[BLANK_AUDIO]" }));
        }
    }

    public class VoiceModelStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "bm-voice-tests", Guid.NewGuid().ToString("N"));
        private static readonly byte[] Model = Enumerable.Range(0, 100_000).Select(i => (byte)(i * 7)).ToArray();
        private static string Sha(byte[] b) { using var s = SHA256.Create(); return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }

        private sealed class Stub : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(Respond(r));
        }

        private VoiceModelStore Store(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            new VoiceModelStore("https://unit.test", () => "bm_test_key", _dir) { HttpHandler = new Stub { Respond = respond } };

        private static VoiceModelStore.ModelInfo Info(byte[] bytes, string sha = null) =>
            new VoiceModelStore.ModelInfo { Name = "ggml-test.bin", Size = bytes.Length, Sha256 = sha ?? Sha(bytes), DownloadUrl = "https://releases.example.com/m.bin" };

        [Fact]
        public void Info_parsing_requires_every_field_and_https()
        {
            var ok = VoiceModelStore.ParseInfo("{\"name\":\"ggml-base.en-q5_1.bin\",\"size\":59721011,\"sha256\":\"d5a3\",\"downloadUrl\":\"https://x/y\"}");
            Assert.Equal("ggml-base.en-q5_1.bin", ok.Name); Assert.Equal(59721011, ok.Size);
            Assert.Throws<InvalidOperationException>(() => VoiceModelStore.ParseInfo("{\"name\":\"a\",\"size\":1,\"sha256\":\"b\",\"downloadUrl\":\"http://x/y\"}"));
            Assert.Throws<InvalidOperationException>(() => VoiceModelStore.ParseInfo("{\"name\":\"a\",\"size\":1,\"sha256\":\"b\"}"));
            var s = new VoiceModelStore("https://unit.test", () => "k", _dir);
            Assert.Throws<ArgumentException>(() => s.PathFor("..\\evil.bin"));
            Assert.Throws<ArgumentException>(() => s.PathFor("sub/evil.bin"));
        }

        [Fact]
        public async Task Download_verifies_hash_then_renames_into_place()
        {
            var store = Store(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Model) });
            var progress = new List<double>();
            var path = await store.EnsureAsync(Info(Model), new Progress<double>(p => { lock (progress) progress.Add(p); }));
            Assert.True(File.Exists(path));
            Assert.Equal(Model, File.ReadAllBytes(path));
            Assert.False(File.Exists(path + ".partial"));
            // second call: already verified on disk, no network
            var store2 = Store(_ => throw new Exception("must not download again"));
            Assert.Equal(path, await store2.EnsureAsync(Info(Model)));
        }

        [Fact]
        public async Task Wrong_hash_or_size_leaves_nothing_on_disk()
        {
            var store = Store(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Model) });
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnsureAsync(Info(Model, sha: "0000")));
            Assert.False(File.Exists(Path.Combine(_dir, "ggml-test.bin")));
            Assert.False(File.Exists(Path.Combine(_dir, "ggml-test.bin.partial")));
            var tooBig = Info(Model); tooBig.Size = Model.Length - 10;
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnsureAsync(tooBig));
            Assert.Empty(Directory.Exists(_dir) ? Directory.GetFiles(_dir) : Array.Empty<string>());
        }

        [Fact]
        public void A_corrupt_file_already_on_disk_is_deleted_not_trusted()
        {
            Directory.CreateDirectory(_dir);
            var p = Path.Combine(_dir, "ggml-test.bin");
            File.WriteAllBytes(p, Model.Take(Model.Length - 1).ToArray()); // truncated
            var store = Store(_ => throw new Exception("no network"));
            Assert.Null(store.ExistingVerified(Info(Model)));
            Assert.False(File.Exists(p));
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    }
}
