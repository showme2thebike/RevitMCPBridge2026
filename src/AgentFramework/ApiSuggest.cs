#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// "Did you mean" for script compile errors (2026-11-12). The model writes Revit API code from memory and, in the
    /// corners, invents member names: eight different ones in one afternoon, each costing a 10-25K-token rewrite because
    /// the compiler's message says only that the name does not exist. This looks the real type up by reflection and
    /// appends the closest actual members or types, so the next rewrite is a one-word fix. Pure: the type universe is
    /// passed in (tests use BCL types; production passes the Revit API assemblies).
    /// </summary>
    public static class ApiSuggest
    {
        static readonly Regex NoMember = new Regex(@"'([^']+)' does not contain a definition for '([^']+)'", RegexOptions.Compiled);
        static readonly Regex NoType   = new Regex(@"type or namespace name '([^']+)' could not be found", RegexOptions.Compiled);
        static readonly Regex NoName   = new Regex(@"The name '([^']+)' does not exist in the current context", RegexOptions.Compiled);

        /// <summary>Append " → Did you mean: …" to each diagnostic this can help with. Others pass through unchanged.</summary>
        public static List<string> Enrich(IEnumerable<string> diagnostics, IReadOnlyList<Type> universe, int max = 5)
        {
            var outList = new List<string>();
            foreach (var d in diagnostics ?? Array.Empty<string>())
            {
                var s = Suggest(d, universe, max);
                outList.Add(s == null ? d : $"{d} → Did you mean: {s}");
            }
            return outList;
        }

        public static string? Suggest(string diagnostic, IReadOnlyList<Type> universe, int max = 5)
        {
            if (string.IsNullOrEmpty(diagnostic) || universe == null || universe.Count == 0) return null;
            var m = NoMember.Match(diagnostic);
            if (m.Success)
            {
                var typeName = BareName(m.Groups[1].Value); var member = m.Groups[2].Value;
                var types = universe.Where(t => string.Equals(TypeBare(t), typeName, StringComparison.Ordinal)).ToList();
                if (types.Count == 0) types = universe.Where(t => string.Equals(TypeBare(t), typeName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (types.Count == 0) return null;
                var members = types.SelectMany(MemberNames).Distinct(StringComparer.Ordinal).ToList();
                var best = Rank(member, members, max);
                if (best.Count == 0) return null;
                var t0 = types[0];
                return string.Join(", ", best) + $" (members of {TypeBare(t0)})";
            }
            var n = NoType.Match(diagnostic); if (!n.Success) n = NoName.Match(diagnostic);
            if (n.Success)
            {
                var name = BareName(n.Groups[1].Value);
                var names = universe.Select(TypeBare).Distinct(StringComparer.Ordinal).ToList();
                var best = Rank(name, names, max);
                if (best.Count == 0) return null;
                var withNs = best.Select(b => { var t = universe.FirstOrDefault(x => TypeBare(x) == b); return t == null ? b : $"{t.Namespace}.{b}"; });
                return string.Join(", ", withNs);
            }
            return null;
        }

        /// <summary>Reflection names carry a generic arity suffix (List`1); scripts and diagnostics do not.</summary>
        static string TypeBare(Type t) { var n = t.Name; var i = n.IndexOf('`'); return i >= 0 ? n.Substring(0, i) : n; }

        static string BareName(string s)
        {
            var i = s.LastIndexOf('.'); if (i >= 0) s = s.Substring(i + 1);
            var g = s.IndexOf('<'); if (g >= 0) s = s.Substring(0, g);
            return s;
        }

        static IEnumerable<string> MemberNames(Type t)
        {
            MemberInfo[] members;
            try { members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy); }
            catch { yield break; }
            foreach (var mi in members)
            {
                var name = mi.Name;
                if (name.StartsWith("get_") || name.StartsWith("set_") || name.StartsWith("add_") || name.StartsWith("remove_") || name.StartsWith("op_") || name == ".ctor" || name == ".cctor") continue;
                yield return name;
            }
        }

        /// <summary>Closest candidates: shared stem or containment first, then edit distance; ties by name.</summary>
        static readonly Regex CamelToken = new Regex(@"[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])", RegexOptions.Compiled);
        static List<string> Tokens(string name) => CamelToken.Matches(name).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();

        /// <summary>
        /// Closest candidates. API names are made of words (Get, Exact, Length), so word overlap is the main signal
        /// (GetLength → GetExactLength before Length); edit distance separates spellings (Monda → Monday); a shared first
        /// word (the verb) nudges. Candidates with no word in common and a large edit distance are dropped rather than
        /// listed as noise.
        /// </summary>
        public static List<string> Rank(string wanted, IEnumerable<string> candidates, int max)
        {
            var w = wanted.ToLowerInvariant(); var wt = Tokens(wanted);
            var scored = new List<(string name, double score)>();
            foreach (var c in candidates)
            {
                if (string.Equals(c, wanted, StringComparison.Ordinal)) continue;
                var cl = c.ToLowerInvariant(); var ct = Tokens(c);
                var shared = wt.Count(t => ct.Contains(t)); var union = wt.Union(ct).Count();
                var jaccard = union == 0 ? 0 : shared / (double)union;
                var ratio = Levenshtein(w, cl) / (double)Math.Max(1, Math.Max(w.Length, cl.Length));
                if (jaccard == 0 && ratio > 0.5) continue;
                var score = (1 - jaccard) * 0.6 + ratio * 0.4;
                if (wt.Count > 0 && ct.Count > 0 && wt.First() == ct.First()) score -= 0.05;
                if (score > 0.85) continue;
                scored.Add((c, score));
            }
            return scored.OrderBy(x => x.score).ThenBy(x => x.name, StringComparer.Ordinal).Select(x => x.name).Take(max).ToList();
        }

        public static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length; if (b.Length == 0) return a.Length;
            var prev = new int[b.Length + 1]; var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        // ------------------------------------------------------------------ production universe
        static IReadOnlyList<Type>? _revitTypes;
        /// <summary>Public types of the loaded Revit API assemblies plus the core BCL ones scripts touch. Cached after first use.</summary>
        public static IReadOnlyList<Type> RevitTypes()
        {
            if (_revitTypes != null) return _revitTypes;
            var list = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name ?? "";
                var wanted = name.StartsWith("RevitAPI", StringComparison.OrdinalIgnoreCase) || name == "System.Private.CoreLib" || name == "System.Linq" || name == "System.Collections";
                if (!wanted) continue;
                Type?[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { continue; }
                foreach (var t in types) if (t != null && t.IsPublic && !t.IsGenericTypeDefinition && !t.Name.StartsWith("<")) list.Add(t);
            }
            _revitTypes = list;
            return list;
        }
    }
}
