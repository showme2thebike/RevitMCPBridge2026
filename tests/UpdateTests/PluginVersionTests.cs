#nullable enable
using System.Linq;
using RevitMCPBridge.Update;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class PluginVersionTests
    {
        [Theory]
        [InlineData("v0.4.20261103", 0, 4, 20261103, '\0')]
        [InlineData("v0.4.20261103a", 0, 4, 20261103, 'a')]
        [InlineData("0.4.20261103", 0, 4, 20261103, '\0')]       // missing "v" tolerated
        [InlineData("  v0.4.20261009z ", 0, 4, 20261009, 'z')]   // surrounding whitespace tolerated
        [InlineData("v1.12.20270101b", 1, 12, 20270101, 'b')]
        public void Parses_well_formed_versions(string text, int major, int minor, int date, char suffix)
        {
            var v = PluginVersion.Parse(text);
            Assert.NotNull(v);
            Assert.Equal(major, v!.Major);
            Assert.Equal(minor, v.Minor);
            Assert.Equal(date, v.Date);
            Assert.Equal(suffix, v.Suffix);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("v0.4")]
        [InlineData("v0.4.2026110")]       // 7 digits
        [InlineData("v0.4.202611030")]     // 9 digits
        [InlineData("v0.4.20261399")]      // not a calendar date
        [InlineData("v0.4.20261103A")]     // uppercase suffix
        [InlineData("v0.4.20261103ab")]    // two-letter suffix
        [InlineData("v0.4.20261103-beta")]
        [InlineData("2.0.0.0")]
        [InlineData("latest")]
        public void Malformed_versions_parse_to_null(string? text)
        {
            Assert.Null(PluginVersion.Parse(text));
        }

        [Fact]
        public void Orders_by_major_minor_date_then_suffix()
        {
            var expected = new[]
            {
                "v0.4.20261009", "v0.4.20261009a", "v0.4.20261009b", "v0.4.20261010",
                "v0.4.20261103", "v0.5.20250101", "v1.0.20200101",
            };
            var shuffled = new[] { "v0.5.20250101", "v0.4.20261009b", "v1.0.20200101", "v0.4.20261010", "v0.4.20261009", "v0.4.20261103", "v0.4.20261009a" };
            var sorted = shuffled.Select(s => PluginVersion.Parse(s)!).OrderBy(v => v).Select(v => v.ToString()).ToArray();
            Assert.Equal(expected, sorted);
        }

        [Fact]
        public void Suffix_is_newer_than_no_suffix_on_same_day()
        {
            var plain = PluginVersion.Parse("v0.4.20261009")!;
            var a = PluginVersion.Parse("v0.4.20261009a")!;
            var b = PluginVersion.Parse("v0.4.20261009b")!;
            Assert.True(a > plain);
            Assert.True(b > a);
            Assert.True(plain < b);
            Assert.True(a >= a);
            Assert.True(a <= a);
            Assert.False(a > a);
        }

        [Fact]
        public void Equality_and_tostring_roundtrip()
        {
            var x = PluginVersion.Parse("0.4.20261103a")!;
            var y = PluginVersion.Parse("v0.4.20261103a")!;
            Assert.Equal(x, y);
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
            Assert.Equal("v0.4.20261103a", x.ToString());
            Assert.Equal("v0.4.20261103", PluginVersion.Parse("v0.4.20261103")!.ToString());
        }
    }
}
