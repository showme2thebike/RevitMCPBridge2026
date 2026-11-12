#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class ApiSuggestTests
    {
        static readonly IReadOnlyList<Type> Universe = new[] { typeof(string), typeof(Stopwatch), typeof(DayOfWeek), typeof(List<int>), typeof(Math), typeof(TimeSpan), typeof(StringComparison) };

        [Fact]
        public void Misspelled_member_gets_the_real_one_first()
        {
            var s = ApiSuggest.Suggest("(3,14): error CS1061: 'Stopwatch' does not contain a definition for 'ElapsedMs' and no accessible extension method 'ElapsedMs'", Universe);
            Assert.NotNull(s);
            Assert.StartsWith("Elapsed", s);                 // Elapsed, ElapsedTicks, ElapsedMilliseconds — all real, all close
            Assert.Contains("ElapsedMilliseconds", s);
            Assert.Contains("members of Stopwatch", s);
        }

        [Fact]
        public void Enum_member_guess_maps_to_the_real_value()
        {
            var s = ApiSuggest.Suggest("error CS0117: 'DayOfWeek' does not contain a definition for 'Monda'", Universe);
            Assert.NotNull(s);
            Assert.StartsWith("Monday", s);
        }

        [Fact]
        public void Invented_member_with_a_shared_stem_is_found_by_containment()
        {
            // the 2026-11-10 case shape: CurveLoop.GetLength() vs the real GetExactLength()
            var best = ApiSuggest.Rank("GetLength", new[] { "GetExactLength", "GetPlane", "Create", "IsOpen", "Length" }, 3);
            Assert.Equal("GetExactLength", best[0]);
        }

        [Fact]
        public void Missing_type_suggests_similar_types_with_namespace()
        {
            var s = ApiSuggest.Suggest("error CS0246: The type or namespace name 'StopWatch' could not be found (are you missing a using directive or an assembly reference?)", Universe);
            Assert.NotNull(s);
            Assert.Contains("System.Diagnostics.Stopwatch", s);
        }

        [Fact]
        public void Unknown_type_or_unrelated_diagnostic_passes_through()
        {
            Assert.Null(ApiSuggest.Suggest("error CS1061: 'NoSuchType' does not contain a definition for 'Foo'", Universe));
            Assert.Null(ApiSuggest.Suggest("error CS1503: Argument 1: cannot convert from 'Material' to 'ElementId'", Universe));
            Assert.Null(ApiSuggest.Suggest("", Universe));
        }

        [Fact]
        public void Enrich_appends_only_where_it_can_help()
        {
            var r = ApiSuggest.Enrich(new[] { "error CS1061: 'string' does not contain a definition for 'Lenght'", "error CS1513: } expected" }, Universe);
            Assert.Equal(2, r.Count);
            Assert.Contains("→ Did you mean: Length", r[0]);
            Assert.Equal("error CS1513: } expected", r[1]);
        }

        [Fact]
        public void Generic_and_qualified_type_names_are_reduced_to_the_bare_name()
        {
            var s = ApiSuggest.Suggest("error CS1061: 'System.Collections.Generic.List<int>' does not contain a definition for 'Containz'", Universe);
            Assert.NotNull(s);
            Assert.StartsWith("Contains", s); // the generic arguments and namespace are stripped before the lookup
        }

        [Fact]
        public void Nothing_within_distance_returns_null_rather_than_noise()
        {
            Assert.Null(ApiSuggest.Suggest("error CS1061: 'TimeSpan' does not contain a definition for 'Qzxwvutsrq'", Universe));
        }
    }
}
