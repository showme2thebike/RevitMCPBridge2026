#nullable enable
using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitMCPBridge.Update
{
    /// <summary>
    /// Plugin version "v0.4.YYYYMMDD[a-z]" (docs/plugin-auto-update.md §1). The leading "v" is
    /// tolerated when missing; anything else malformed parses to null. Ordering: major, minor,
    /// date, then suffix where "no suffix" sorts before "a".
    /// </summary>
    public sealed class PluginVersion : IComparable<PluginVersion>, IEquatable<PluginVersion>
    {
        private static readonly Regex Pattern =
            new Regex(@"^v?(\d+)\.(\d+)\.(\d{8})([a-z])?$", RegexOptions.CultureInvariant);

        public int Major { get; }
        public int Minor { get; }
        /// <summary>Build date as an integer, e.g. 20261103.</summary>
        public int Date { get; }
        /// <summary>Optional single lowercase letter; '\0' when absent.</summary>
        public char Suffix { get; }

        private PluginVersion(int major, int minor, int date, char suffix)
        {
            Major = major; Minor = minor; Date = date; Suffix = suffix;
        }

        /// <summary>Returns null for anything that is not a well-formed plugin version.</summary>
        public static PluginVersion? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Pattern.Match(text!.Trim());
            if (!m.Success) return null;
            if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)) return null;
            if (!int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)) return null;
            var dateText = m.Groups[3].Value;
            if (!DateTime.TryParseExact(dateText, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return null;
            var date = int.Parse(dateText, CultureInfo.InvariantCulture);
            var suffix = m.Groups[4].Success ? m.Groups[4].Value[0] : '\0';
            return new PluginVersion(major, minor, date, suffix);
        }

        private static readonly Lazy<PluginVersion?> _installed = new Lazy<PluginVersion?>(() =>
        {
            try
            {
                var attr = typeof(PluginVersion).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return Parse(attr?.InformationalVersion);
            }
            catch { return null; }
        });

        /// <summary>The running plugin's version from AssemblyInformationalVersion; null if unreadable.</summary>
        public static PluginVersion? Installed => _installed.Value;

        public int CompareTo(PluginVersion? other)
        {
            if (other is null) return 1;
            var c = Major.CompareTo(other.Major); if (c != 0) return c;
            c = Minor.CompareTo(other.Minor); if (c != 0) return c;
            c = Date.CompareTo(other.Date); if (c != 0) return c;
            return Suffix.CompareTo(other.Suffix); // no suffix sorts before "a", "a" before "b"
        }

        public bool Equals(PluginVersion? other) => other is not null && CompareTo(other) == 0;
        public override bool Equals(object? obj) => obj is PluginVersion v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(Major, Minor, Date, Suffix);

        public override string ToString() =>
            Suffix == '\0'
                ? FormattableString.Invariant($"v{Major}.{Minor}.{Date:D8}")
                : FormattableString.Invariant($"v{Major}.{Minor}.{Date:D8}{Suffix}");

        public static bool operator >(PluginVersion a, PluginVersion b) => a.CompareTo(b) > 0;
        public static bool operator <(PluginVersion a, PluginVersion b) => a.CompareTo(b) < 0;
        public static bool operator >=(PluginVersion a, PluginVersion b) => a.CompareTo(b) >= 0;
        public static bool operator <=(PluginVersion a, PluginVersion b) => a.CompareTo(b) <= 0;
    }
}
