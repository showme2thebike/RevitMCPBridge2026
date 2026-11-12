#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class ApiSuggestMoreTests
    {
        static readonly IReadOnlyList<Type> Universe = new[] { typeof(string), typeof(Stopwatch), typeof(DayOfWeek), typeof(List<int>), typeof(TimeSpan) };

        [Fact]
        public void Wrong_case_is_auto_corrected()
        {
            var c = ApiSuggest.AutoCorrect("error CS1061: 'Stopwatch' does not contain a definition for 'elapsedMilliseconds'", Universe);
            Assert.NotNull(c);
            Assert.Equal("ElapsedMilliseconds", c!.Value.Right);
            Assert.Equal("Stopwatch", c.Value.Type);
        }

        [Fact]
        public void A_single_member_within_two_edits_is_auto_corrected()
        {
            var c = ApiSuggest.AutoCorrect("error CS0117: 'DayOfWeek' does not contain a definition for 'Wendesday'", Universe);
            Assert.NotNull(c);
            Assert.Equal("Wednesday", c!.Value.Right);
        }

        public sealed class Twins { public int Foo1 => 1; public int Foo2 => 2; public int GetLayerWidth(int i) => i; }

        [Fact]
        public void Ambiguous_distant_short_or_existing_names_are_not_auto_corrected()
        {
            var u = new List<Type>(Universe) { typeof(Twins) };
            // too short to risk (< 5 chars)
            Assert.Null(ApiSuggest.AutoCorrect("error CS1061: 'Stopwatch' does not contain a definition for 'Elap'", u));
            // Elapsedness → Elapsed is 4 edits: a suggestion at most, never a silent rewrite
            Assert.Null(ApiSuggest.AutoCorrect("error CS1061: 'Stopwatch' does not contain a definition for 'Elapsedness'", u));
            // Foo12 is one edit from BOTH Foo1 and Foo2: ambiguous
            Assert.Null(ApiSuggest.AutoCorrect("error CS1061: 'Twins' does not contain a definition for 'Foo12'", u));
            // the name exists as written (the compiler is unhappy about an overload or access, not the spelling)
            Assert.Null(ApiSuggest.AutoCorrect("error CS1061: 'Twins' does not contain a definition for 'GetLayerWidth'", u));
            Assert.Null(ApiSuggest.AutoCorrect("error CS1513: } expected", u));
        }

        [Fact]
        public void ApplyCorrections_replaces_only_member_tokens_after_a_dot()
        {
            var code = "var t = sw.elapsedMilliseconds; var elapsedMilliseconds = 3; Console.Write(x.elapsedMilliseconds2);";
            var fixedCode = ApiSuggest.ApplyCorrections(code, new[] { new ApiSuggest.Correction("Stopwatch", "elapsedMilliseconds", "ElapsedMilliseconds") });
            Assert.Equal("var t = sw.ElapsedMilliseconds; var elapsedMilliseconds = 3; Console.Write(x.elapsedMilliseconds2);", fixedCode);
        }

        [Fact]
        public void AutoCorrections_dedupes_by_wrong_name()
        {
            var diags = new[] {
                "(1,1): error CS1061: 'Stopwatch' does not contain a definition for 'elapsedMilliseconds'",
                "(2,1): error CS1061: 'Stopwatch' does not contain a definition for 'elapsedMilliseconds'",
            };
            Assert.Single(ApiSuggest.AutoCorrections(diags, Universe));
        }

        [Fact]
        public void MemberList_returns_sorted_real_members_or_null()
        {
            var list = ApiSuggest.MemberList("Stopwatch", Universe);
            Assert.NotNull(list);
            Assert.Contains("ElapsedMilliseconds", list!);
            Assert.Contains("Start", list);
            Assert.Null(ApiSuggest.MemberList("NoSuchType", Universe));
        }

        [Fact]
        public void Enrich_falls_back_to_the_member_list_when_nothing_is_close()
        {
            var r = ApiSuggest.Enrich(new[] { "error CS1061: 'TimeSpan' does not contain a definition for 'Qzxwvutsrq'" }, Universe);
            Assert.Contains("No close match. Members of TimeSpan:", r[0]);
            Assert.Contains("Ticks", r[0]);
            Assert.Contains("more (call describeApi(\"TimeSpan\") for all)", r[0]);   // TimeSpan has > 40 members
        }

        [Fact]
        public void Describe_lists_properties_methods_and_enum_values()
        {
            var sw = ApiSuggest.Describe("Stopwatch", Universe);
            Assert.StartsWith("System.Diagnostics.Stopwatch", sw);
            Assert.Contains("ElapsedMilliseconds: Int64 (read-only)", sw);
            Assert.Contains("Start(): Void", sw);
            var dow = ApiSuggest.Describe("DayOfWeek", Universe);
            Assert.Contains("(enum)", dow);
            Assert.Contains("Monday", dow);
            var miss = ApiSuggest.Describe("StopWatch", Universe); // case-insensitive hit
            Assert.StartsWith("System.Diagnostics.Stopwatch", miss);
            var none = ApiSuggest.Describe("Stopwach", Universe);  // close but wrong → closest real types
            Assert.Contains("Closest real types", none);
            Assert.Contains("Stopwatch", none);
        }
    }
}
