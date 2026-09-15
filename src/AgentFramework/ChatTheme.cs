using System;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Banana Chat colour palette that follows Revit's UI theme (Light / Dark).
    ///
    /// Every brush here is a single shared, unfrozen SolidColorBrush. Controls hold a
    /// reference to the brush, so changing the brush's Color re-paints every control that
    /// uses it — no visual-tree walk is needed when Revit switches theme.
    ///
    /// Initial theme is read from UIThemeManager.CurrentTheme (Revit 2024+). Live switching
    /// is driven by the ThemeChanged event, which RevitMCPBridgeApp forwards to Apply().
    /// If the API is unavailable for any reason the panel stays dark, exactly as before.
    /// </summary>
    public static class ChatTheme
    {
        // ── Surfaces ─────────────────────────────────────────────────────────
        public static readonly SolidColorBrush Bg         = new SolidColorBrush();
        public static readonly SolidColorBrush Surface    = new SolidColorBrush();
        public static readonly SolidColorBrush SurfaceAlt = new SolidColorBrush();
        public static readonly SolidColorBrush Input      = new SolidColorBrush();
        public static readonly SolidColorBrush Neutral    = new SolidColorBrush();   // neutral button bg (white text on it)
        public static readonly SolidColorBrush Border     = new SolidColorBrush();
        public static readonly SolidColorBrush Disabled   = new SolidColorBrush();
        public static readonly SolidColorBrush Selection  = new SolidColorBrush();

        // ── Text ─────────────────────────────────────────────────────────────
        public static readonly SolidColorBrush TextPrimary   = new SolidColorBrush();
        public static readonly SolidColorBrush TextSecondary = new SolidColorBrush();
        public static readonly SolidColorBrush TextMuted     = new SolidColorBrush();
        public static readonly SolidColorBrush CmdBuiltin    = new SolidColorBrush();
        public static readonly SolidColorBrush CmdCustom     = new SolidColorBrush();

        // ── Semantic tints ───────────────────────────────────────────────────
        public static readonly SolidColorBrush Green         = new SolidColorBrush();
        public static readonly SolidColorBrush SuccessBg     = new SolidColorBrush();
        public static readonly SolidColorBrush SuccessBorder = new SolidColorBrush();
        public static readonly SolidColorBrush SuccessText   = new SolidColorBrush();
        public static readonly SolidColorBrush ErrorBg       = new SolidColorBrush();
        public static readonly SolidColorBrush ErrorText     = new SolidColorBrush();
        public static readonly SolidColorBrush WarnBg        = new SolidColorBrush();
        public static readonly SolidColorBrush WarnBorder    = new SolidColorBrush();
        public static readonly SolidColorBrush WarnText      = new SolidColorBrush();

        /// <summary>Colour values for code paths that need a Color rather than a Brush (hover animations).</summary>
        public static Color NeutralColor => Neutral.Color;
        public static Color HoverColor   => IsDark ? Color.FromRgb(105, 105, 105) : Color.FromRgb(150, 150, 150);

        public static bool IsDark { get; private set; } = true;

        /// <summary>Raised after a theme has been applied (on the UI thread).</summary>
        public static event Action<bool> Changed;

        static ChatTheme()
        {
            bool dark = true;
            try { dark = UIThemeManager.CurrentTheme == UITheme.Dark; }
            catch (Exception ex) { Log.Debug(ex, "ChatTheme: UIThemeManager unavailable, defaulting to dark"); }
            Apply(dark);
        }

        /// <summary>Re-read Revit's current UI theme and apply it.</summary>
        public static void SyncWithRevit()
        {
            try { Apply(UIThemeManager.CurrentTheme == UITheme.Dark); }
            catch (Exception ex) { Log.Debug(ex, "ChatTheme: could not read Revit theme"); }
        }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            if (dark)
            {
                Set(Bg,         30, 30, 30);
                Set(Surface,    45, 45, 45);
                Set(SurfaceAlt, 38, 38, 38);
                Set(Input,      60, 60, 60);
                Set(Neutral,    85, 85, 85);
                Set(Border,     85, 85, 85);
                Set(Disabled,   60, 60, 60);
                Set(Selection,  60, 100, 160);

                Set(TextPrimary,   255, 255, 255);
                Set(TextSecondary, 160, 160, 160);
                Set(TextMuted,     110, 110, 110);
                Set(CmdBuiltin,    130, 180, 255);
                Set(CmdCustom,     180, 230, 130);

                Set(Green,         100, 180, 100);
                Set(SuccessBg,     30, 55, 30);
                Set(SuccessBorder, 60, 110, 60);
                Set(SuccessText,   160, 220, 160);
                Set(ErrorBg,       60, 30, 30);
                Set(ErrorText,     240, 110, 110);
                Set(WarnBg,        40, 32, 10);
                Set(WarnBorder,    160, 120, 30);
                Set(WarnText,      210, 180, 100);
            }
            else
            {
                Set(Bg,         250, 250, 250);
                Set(Surface,    235, 235, 235);
                Set(SurfaceAlt, 242, 242, 242);
                Set(Input,      255, 255, 255);
                Set(Neutral,    130, 130, 130);
                Set(Border,     205, 205, 205);
                Set(Disabled,   200, 200, 200);
                Set(Selection,  200, 220, 245);

                Set(TextPrimary,   25, 25, 25);
                Set(TextSecondary, 85, 85, 85);
                Set(TextMuted,     140, 140, 140);
                Set(CmdBuiltin,    30, 90, 200);
                Set(CmdCustom,     60, 130, 20);

                Set(Green,         40, 130, 40);
                Set(SuccessBg,     225, 243, 225);
                Set(SuccessBorder, 120, 180, 120);
                Set(SuccessText,   30, 110, 30);
                Set(ErrorBg,       252, 230, 230);
                Set(ErrorText,     190, 40, 40);
                Set(WarnBg,        255, 247, 222);
                Set(WarnBorder,    200, 160, 60);
                Set(WarnText,      140, 100, 20);
            }
            try { Changed?.Invoke(dark); } catch (Exception ex) { Log.Debug(ex, "ChatTheme.Changed handler failed"); }
        }

        private static void Set(SolidColorBrush b, byte r, byte g, byte bl)
        {
            var c = Color.FromRgb(r, g, bl);
            if (b.Dispatcher != null && !b.Dispatcher.CheckAccess())
                b.Dispatcher.Invoke(() => b.Color = c);
            else
                b.Color = c;
        }
    }
}
