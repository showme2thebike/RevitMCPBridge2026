using System.Collections.Generic;
using System.Threading.Tasks;
using RevitMCPBridge.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    // KnowledgeStore is the in-memory replacement for the shipped, shared-key-encrypted corpus.
    // These tests cover what must hold without a network: name hygiene mirrors the server, the
    // send path never blocks, and the local fallback keeps a transitional build whole.
    [Collection("KnowledgeStore")]
    public class KnowledgeStoreTests
    {
        public KnowledgeStoreTests()
        {
            KnowledgeStore.ResetForTests();
            KnowledgeStore.ApiKey = null;        // no network in these tests
            KnowledgeStore.LocalFallback = null;
        }

        [Theory]
        [InlineData("cad-visual-rules", "cad-visual-rules.md")]
        [InlineData("cad-visual-rules.md", "cad-visual-rules.md")]
        [InlineData("  _index.md ", "_index.md")]
        [InlineData("WHAT_ACTUALLY_WORKS", "WHAT_ACTUALLY_WORKS.md")]
        [InlineData("revit-api-wall-roof-2026", "revit-api-wall-roof-2026.md")]
        public void Normalize_accepts_flat_names_and_appends_extension(string input, string expected)
        {
            Assert.Equal(expected, KnowledgeStore.Normalize(input));
        }

        [Theory]
        [InlineData("../index.js")]
        [InlineData("..\\secrets.md")]
        [InlineData("sub/file.md")]
        [InlineData("file.md.bak")]
        [InlineData(".hidden")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("name with space")]
        public void Normalize_refuses_traversal_and_odd_names_instead_of_repairing_them(string input)
        {
            Assert.Null(KnowledgeStore.Normalize(input));
        }

        [Fact]
        public void GetOrLocal_uses_fallback_when_nothing_cached_and_null_when_no_fallback()
        {
            Assert.Null(KnowledgeStore.GetOrLocal("annotation-standards"));
            KnowledgeStore.LocalFallback = n => n == "annotation-standards.md" ? "LOCAL TEXT" : null;
            Assert.Equal("LOCAL TEXT", KnowledgeStore.GetOrLocal("annotation-standards"));
            Assert.Null(KnowledgeStore.GetOrLocal("something-else"));
        }

        [Fact]
        public void GetOrLocal_swallows_a_throwing_fallback()
        {
            KnowledgeStore.LocalFallback = _ => throw new System.IO.IOException("disk gone");
            Assert.Null(KnowledgeStore.GetOrLocal("annotation-standards"));
        }

        [Fact]
        public async Task GetAsync_without_api_key_never_touches_network_and_falls_back_locally()
        {
            KnowledgeStore.LocalFallback = n => "FROM DISK: " + n;
            var text = await KnowledgeStore.GetAsync("error-recovery");
            Assert.Equal("FROM DISK: error-recovery.md", text);
            Assert.Equal(0, KnowledgeStore.CachedCount);   // local reads are not cached as server copies
        }

        [Fact]
        public async Task GetAsync_refuses_bad_names_before_any_io()
        {
            KnowledgeStore.LocalFallback = _ => "SHOULD NOT BE CALLED";
            Assert.Null(await KnowledgeStore.GetAsync("../etc/passwd"));
        }

        [Fact]
        public async Task Preload_without_api_key_is_a_no_op_but_marks_preloaded()
        {
            Assert.False(KnowledgeStore.Preloaded);
            await KnowledgeStore.PreloadAsync(new List<string> { "_index.md", "error-recovery.md" });
            Assert.True(KnowledgeStore.Preloaded);
            Assert.Equal(0, KnowledgeStore.CachedCount);
            Assert.Empty(KnowledgeStore.RemoteNames);
        }

        [Fact]
        public void Disk_cache_round_trips_through_DPAPI_and_is_ignored_when_corrupt()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bm-ks-" + System.Guid.NewGuid().ToString("N") + ".bin");
            var original = KnowledgeStore.CachePath;
            try
            {
                KnowledgeStore.CachePath = path;
                // Nothing cached → nothing written.
                KnowledgeStore.SaveCacheToDisk();
                Assert.False(System.IO.File.Exists(path));

                // Seed via the local fallback path, then persist. (GetOrLocal does not cache; use the
                // internal seam: a fake "server" result is what PreloadAsync would have put in.)
                KnowledgeStore.ResetForTests();
                KnowledgeStore.LocalFallback = null;
                // Simulate a successful preload by loading a cache we construct ourselves:
                var seed = new Newtonsoft.Json.Linq.JObject { ["error-recovery.md"] = "RECOVER", ["_index.md"] = "INDEX" };
                var sealedBytes = System.Security.Cryptography.ProtectedData.Protect(
                    System.Text.Encoding.UTF8.GetBytes(seed.ToString(Newtonsoft.Json.Formatting.None)), null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                System.IO.File.WriteAllBytes(path, sealedBytes);

                Assert.Equal(2, KnowledgeStore.LoadCacheFromDisk());
                Assert.Equal("RECOVER", KnowledgeStore.GetOrLocal("error-recovery"));
                Assert.Equal("INDEX", KnowledgeStore.TryGetCached("_index.md"));

                // Now the store writes its own cache; it must be unreadable as plaintext and reload cleanly.
                KnowledgeStore.SaveCacheToDisk();
                var raw = System.IO.File.ReadAllBytes(path);
                Assert.DoesNotContain("RECOVER", System.Text.Encoding.UTF8.GetString(raw));
                KnowledgeStore.ResetForTests();
                Assert.Equal(2, KnowledgeStore.LoadCacheFromDisk());

                // Corrupt file → ignored, nothing loaded, no throw.
                KnowledgeStore.ResetForTests();
                System.IO.File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5 });
                Assert.Equal(0, KnowledgeStore.LoadCacheFromDisk());
                Assert.Equal(0, KnowledgeStore.CachedCount);
            }
            finally
            {
                KnowledgeStore.CachePath = original;
                try { System.IO.File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void Reset_clears_everything()
        {
            KnowledgeStore.LocalFallback = n => "x";
            Assert.Equal("x", KnowledgeStore.GetOrLocal("a"));
            KnowledgeStore.ResetForTests();
            Assert.False(KnowledgeStore.Preloaded);
            Assert.Equal(0, KnowledgeStore.CachedCount);
        }
    }

    public class ProtectedSecretTests
    {
        [Fact]
        public void Round_trips_and_is_not_plaintext_on_disk()
        {
            var sealed_ = ProtectedSecret.Protect("sk-ant-api03-example-not-a-real-key");
            Assert.StartsWith("dpapi:", sealed_);
            Assert.DoesNotContain("sk-ant", sealed_);
            Assert.True(ProtectedSecret.IsProtected(sealed_));
            Assert.Equal("sk-ant-api03-example-not-a-real-key", ProtectedSecret.Unprotect(sealed_));
        }

        [Fact]
        public void Plaintext_passes_through_so_old_configs_migrate_on_next_save()
        {
            Assert.False(ProtectedSecret.IsProtected("sk-ant-plain"));
            Assert.Equal("sk-ant-plain", ProtectedSecret.Unprotect("sk-ant-plain"));
        }

        [Fact]
        public void Empty_corrupt_and_foreign_values_read_as_absent()
        {
            Assert.Null(ProtectedSecret.Protect(""));
            Assert.Null(ProtectedSecret.Protect(null));
            Assert.Null(ProtectedSecret.Unprotect(null));
            Assert.Null(ProtectedSecret.Unprotect("dpapi:not-base64!!"));
            Assert.Null(ProtectedSecret.Unprotect("dpapi:" + System.Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));
        }
    }

    public class PluginIdentityTests
    {
        [Fact]
        public void Capabilities_are_the_strings_the_server_contract_expects()
        {
            // inferenceProxy.js: CAP_BYOK_PASSTHROUGH / CAP_KNOWLEDGE_API
            Assert.Contains("byok-passthrough", PluginIdentity.Capabilities.Split(','));
            Assert.Contains("knowledge-api", PluginIdentity.Capabilities.Split(','));
            Assert.Equal("X-Plugin-Capabilities", PluginIdentity.CapabilitiesHeader);
            Assert.Equal("X-Plugin-Version", PluginIdentity.VersionHeader);
        }

        [Fact]
        public void Stamp_sets_both_headers_and_is_idempotent()
        {
            using var client = new System.Net.Http.HttpClient();
            PluginIdentity.Stamp(client.DefaultRequestHeaders);
            PluginIdentity.Stamp(client.DefaultRequestHeaders);
            Assert.Equal(new[] { PluginIdentity.Capabilities }, client.DefaultRequestHeaders.GetValues(PluginIdentity.CapabilitiesHeader));
            Assert.Single(client.DefaultRequestHeaders.GetValues(PluginIdentity.VersionHeader));
            Assert.False(string.IsNullOrEmpty(PluginIdentity.Version));
        }

        [Fact]
        public void Stamp_on_WebHeaderCollection_sets_both()
        {
            var h = new System.Net.WebHeaderCollection();
            PluginIdentity.Stamp(h);
            Assert.Equal(PluginIdentity.Capabilities, h[PluginIdentity.CapabilitiesHeader]);
            Assert.Equal(PluginIdentity.Version, h[PluginIdentity.VersionHeader]);
        }
    }
}
