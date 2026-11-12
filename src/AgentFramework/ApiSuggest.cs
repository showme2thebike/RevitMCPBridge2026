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
                if (s == null)
                {
                    // nothing close: hand over the type's real surface instead of nothing (item 7c)
                    var m = NoMember.Match(d);
                    var list = m.Success ? MemberList(m.Groups[1].Value, universe, int.MaxValue) : null;
                    if (list == null || list.Count == 0) { outList.Add(d); continue; }
                    var tn = BareName(m.Groups[1].Value);
                    var tail = list.Count > MemberListCap ? $", … {list.Count - MemberListCap} more (call describeApi(\"{tn}\") for all)" : "";
                    outList.Add($"{d} → No close match. Members of {tn}: {string.Join(", ", list.Take(MemberListCap))}{tail}");
                    continue;
                }
                outList.Add($"{d} → Did you mean: {s}");
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

        // ------------------------------------------------------------------ auto-correct (2026-11-12, item 7b)

        public readonly struct Correction
        {
            public Correction(string type, string wrong, string right) { Type = type; Wrong = wrong; Right = right; }
            public string Type { get; } public string Wrong { get; } public string Right { get; }
            public override string ToString() => $"{Wrong}→{Right}";
        }

        /// <summary>
        /// A correction the engine may apply WITHOUT asking the model: the member exists under a different case, or there
        /// is exactly one real member within two edits and nothing else close. Anything looser stays a suggestion, because
        /// substituting GetWidth for GetLayerWidth would silently change what the script does.
        /// </summary>
        public static Correction? AutoCorrect(string diagnostic, IReadOnlyList<Type> universe)
        {
            var m = NoMember.Match(diagnostic ?? "");
            if (!m.Success || universe == null) return null;
            var typeName = BareName(m.Groups[1].Value); var wrong = m.Groups[2].Value;
            var types = universe.Where(t => string.Equals(TypeBare(t), typeName, StringComparison.Ordinal)).ToList();
            if (types.Count == 0) return null;
            var members = types.SelectMany(MemberNames).Distinct(StringComparer.Ordinal).ToList();
            if (members.Contains(wrong, StringComparer.Ordinal)) return null; // exists as written: the error is elsewhere (overload, extension, access)
            var ci = members.Where(x => string.Equals(x, wrong, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ci.Count == 1) return new Correction(typeName, wrong, ci[0]);
            if (wrong.Length < 5) return null;
            var close = members.Where(x => Levenshtein(x.ToLowerInvariant(), wrong.ToLowerInvariant()) <= 2).ToList();
            return close.Count == 1 ? new Correction(typeName, wrong, close[0]) : null;
        }

        public static List<Correction> AutoCorrections(IEnumerable<string> diagnostics, IReadOnlyList<Type> universe)
        {
            var list = new List<Correction>();
            foreach (var d in diagnostics ?? Array.Empty<string>())
            {
                var c = AutoCorrect(d, universe);
                if (c != null && !list.Any(x => x.Wrong == c.Value.Wrong)) list.Add(c.Value);
            }
            return list;
        }

        /// <summary>Replace each wrong member token (word boundary, immediately after a '.') with the right one.</summary>
        public static string ApplyCorrections(string code, IEnumerable<Correction> corrections)
        {
            var s = code ?? "";
            foreach (var c in corrections) s = Regex.Replace(s, @"(?<=\.)" + Regex.Escape(c.Wrong) + @"\b", c.Right);
            return s;
        }

        // ------------------------------------------------------------------ describe / member lists (item 7a, 7c)

        /// <summary>All public member names of a type, sorted; null when the type is not in the universe.</summary>
        public const int MemberListCap = 40;
        public static List<string>? MemberList(string typeName, IReadOnlyList<Type> universe, int cap = MemberListCap)
        {
            var bare = BareName(typeName ?? "");
            var types = universe.Where(t => string.Equals(TypeBare(t), bare, StringComparison.OrdinalIgnoreCase)).ToList();
            if (types.Count == 0) return null;
            return types.SelectMany(MemberNames).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).Take(cap).ToList();
        }

        /// <summary>
        /// What the describeApi tool returns: the type's real surface — properties with their types, methods with their
        /// parameter types, enum values — so the model checks instead of guessing. Unknown type → closest real type names.
        /// </summary>
        public static string Describe(string typeName, IReadOnlyList<Type> universe, int cap = 80)
        {
            var bare = BareName(typeName ?? "");
            if (string.IsNullOrWhiteSpace(bare)) return "typeName is required";
            var types = universe.Where(t => string.Equals(TypeBare(t), bare, StringComparison.Ordinal)).ToList();
            if (types.Count == 0) types = universe.Where(t => string.Equals(TypeBare(t), bare, StringComparison.OrdinalIgnoreCase)).ToList();
            if (types.Count == 0)
            {
                var close = Rank(bare, universe.Select(TypeBare).Distinct(StringComparer.Ordinal), 8);
                return close.Count == 0 ? $"No type named {bare} in the loaded Revit API." : $"No type named {bare}. Closest real types: {string.Join(", ", close.Select(c => { var t = universe.FirstOrDefault(x => TypeBare(x) == c); return t == null ? c : t.Namespace + "." + c; }))}.";
            }
            var t0 = types[0];
            var lines = new List<string> { $"{t0.Namespace}.{TypeBare(t0)}" + (t0.BaseType != null && t0.BaseType != typeof(object) ? $" : {TypeBare(t0.BaseType)}" : "") + (t0.IsEnum ? " (enum)" : "") };
            if (t0.IsEnum)
            {
                var names = Enum.GetNames(t0);
                lines.Add($"values ({names.Length}): " + string.Join(", ", names.Take(cap)) + (names.Length > cap ? $", … {names.Length - cap} more" : ""));
                return string.Join("\n", lines);
            }
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            PropertyInfo[] props; MethodInfo[] methods;
            try { props = t0.GetProperties(flags); methods = t0.GetMethods(flags); } catch { return lines[0]; }
            var propLines = props.Select(p => $"{p.Name}: {TypeBare(p.PropertyType)}{(p.CanWrite ? "" : " (read-only)")}").Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var methLines = methods.Where(mi => !mi.IsSpecialName).Select(mi => $"{mi.Name}({string.Join(", ", mi.GetParameters().Select(p => TypeBare(p.ParameterType) + " " + p.Name))}): {TypeBare(mi.ReturnType)}{(mi.IsStatic ? " [static]" : "")}").Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            lines.Add($"properties ({propLines.Count}): " + string.Join("; ", propLines.Take(cap)) + (propLines.Count > cap ? $"; … {propLines.Count - cap} more" : ""));
            lines.Add($"methods ({methLines.Count}): " + string.Join("; ", methLines.Take(cap)) + (methLines.Count > cap ? $"; … {methLines.Count - cap} more" : ""));
            return string.Join("\n", lines);
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
