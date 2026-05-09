using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitMCPBridge.Commands
{
    public class OccupancyRecord
    {
        public string RoomName    { get; set; }
        public string Level       { get; set; }
        public double AreaSqFt    { get; set; }
        public string IbcCategory { get; set; }
        public double LoadFactor  { get; set; }  // 0 = no occupant load
        public string LoadType    { get; set; }  // "net", "gross", "N/A"
        public int    OccupantLoad { get; set; }
    }

    public class OccupancyAnalysis
    {
        public string              ProjectName { get; set; }
        public List<OccupancyRecord> Records   { get; set; } = new List<OccupancyRecord>();

        public int TotalOccupants => Records.Sum(r => r.OccupantLoad);

        private static int RequiredExits(int load)
        {
            if (load <= 0)   return 0;
            if (load <= 499) return 2;
            if (load <= 999) return 3;
            return 4;
        }

        public string FormatForPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"OCCUPANT LOAD ANALYSIS — {ProjectName}");
            sb.AppendLine($"Generated {DateTime.Today:MMMM d, yyyy} | IBC 2021 Table 1004.5");
            sb.AppendLine();

            var byLevel = Records.GroupBy(r => r.Level).OrderBy(g => g.Key);
            foreach (var level in byLevel)
            {
                sb.AppendLine($"── {level.Key} ─────────────────────────────");
                sb.AppendLine($"{"Room",-30} {"Area (sf)",8} {"IBC Category",-28} {"Factor",7} {"Type",6} {"Load",5}");
                sb.AppendLine(new string('─', 90));
                foreach (var r in level.OrderBy(r => r.RoomName))
                {
                    var load = r.OccupantLoad > 0 ? r.OccupantLoad.ToString() : "—";
                    var factor = r.LoadType == "N/A" ? "—" : $"{r.LoadFactor:0}";
                    sb.AppendLine($"{r.RoomName,-30} {r.AreaSqFt,8:N0} {r.IbcCategory,-28} {factor,7} {r.LoadType,6} {load,5}");
                }
                var levelTotal = level.Sum(r => r.OccupantLoad);
                var exits = RequiredExits(levelTotal);
                sb.AppendLine($"{"LEVEL TOTAL:",-30} {"",8} {"",28} {"",7} {"",6} {levelTotal,5}");
                sb.AppendLine($"  → Minimum exits required this level (IBC §1006): {exits}");
                sb.AppendLine();
            }

            sb.AppendLine($"BUILDING TOTAL: {TotalOccupants} occupants");
            sb.AppendLine();
            sb.AppendLine("Note: Load factors applied per IBC 2021 Table 1004.5. Room-to-category");
            sb.AppendLine("matching is based on room names — verify any flagged as (default).");
            sb.AppendLine("Net areas are approximated as gross; refine if precise net areas are available.");
            return sb.ToString().TrimEnd();
        }

        // IBC Table 1004.5 occupancy classification by room name keyword matching
        public static (string category, double factor, string loadType) ClassifyRoom(string roomName)
        {
            var n = (roomName ?? "").ToUpperInvariant();

            // Stairs, shafts, toilets — no occupant load
            if (ContainsAny(n, "STAIR", "STAIRWELL", "ELEVATOR", "SHAFT", "MECHANICAL", "ELECTRICAL",
                               "TELECOM", "SERVER", "IT ROOM", "JANITOR", "CUSTODIAL"))
                return ("Mechanical/utility — no load", 0, "N/A");
            if (ContainsAny(n, "RESTROOM", "TOILET", "BATHROOM", "SHOWER", "LOCKER", "WASHROOM"))
                return ("Toilet/shower — no load", 0, "N/A");

            // Assembly
            if (ContainsAny(n, "AUDITORIUM", "THEATER", "THEATRE", "CHAPEL", "SANCTUARY", "WORSHIP", "ARENA"))
                return ("Assembly — fixed seating", 7, "net");
            if (ContainsAny(n, "CONFERENCE", "MEETING", "BOARD ROOM", "SEMINAR", "TRAINING"))
                return ("Assembly — unconcentrated", 15, "net");
            if (ContainsAny(n, "LOBBY", "WAITING", "RECEPTION", "ATRIUM", "FOYER"))
                return ("Assembly — standing/waiting", 15, "gross");
            if (ContainsAny(n, "DINING", "CAFETERIA", "RESTAURANT", "LUNCHROOM", "BREAK ROOM", "BREAKROOM"))
                return ("Assembly — dining", 15, "net");
            if (ContainsAny(n, "GYM", "FITNESS", "EXERCISE", "RECREATION", "SPORT", "WEIGHT ROOM"))
                return ("Exercise room", 50, "gross");

            // Educational
            if (ContainsAny(n, "CLASSROOM", "LECTURE", "LEARNING", "STUDY HALL"))
                return ("Educational", 20, "net");
            if (ContainsAny(n, "LIBRARY READING", "READING ROOM"))
                return ("Library — reading", 50, "net");
            if (ContainsAny(n, "LIBRARY STACK", "BOOK STACK", "STACKS"))
                return ("Library — stacks", 100, "gross");
            if (ContainsAny(n, "LIBRARY"))
                return ("Library — reading", 50, "net");

            // Residential
            if (ContainsAny(n, "BEDROOM", "SLEEPING", "DORMITORY", "DORM", "UNIT", "APARTMENT", "DWELLING",
                               "SUITE", "LIVING ROOM", "LIVING AREA"))
                return ("Residential", 200, "gross");

            // Kitchen
            if (ContainsAny(n, "KITCHEN", "COOKING", "PREP ROOM", "FOOD PREP"))
                return ("Kitchen — commercial", 200, "gross");

            // Storage
            if (ContainsAny(n, "STORAGE", "STOR.", "WAREHOUSE", "STOCKROOM", "CLOSET", "UTILITY"))
                return ("Storage", 300, "gross");

            // Retail/mercantile
            if (ContainsAny(n, "RETAIL", "SALES", "SHOP", "STORE", "MARKET", "SHOWROOM", "MERCHANDISE"))
                return ("Mercantile", 60, "gross");

            // Corridor/circulation (no assigned load — counted via rooms)
            if (ContainsAny(n, "CORRIDOR", "HALLWAY", "HALL", "PASSAGE", "VESTIBULE"))
                return ("Corridor — no assigned load", 0, "N/A");

            // Business (default for offices and unnamed rooms)
            if (ContainsAny(n, "OFFICE", "OPEN OFFICE", "WORKROOM", "ADMIN", "WORK AREA", "WORKSPACE",
                               "STUDIO", "COPY", "MAIL ROOM", "MAILROOM", "COWORK"))
                return ("Business", 150, "gross");

            // Default
            return ("Business (default)", 150, "gross");
        }

        private static bool ContainsAny(string s, params string[] terms)
            => terms.Any(t => s.Contains(t));
    }

    public class OccupancyDialog : Window
    {
        private StackPanel _tablePanel;
        private TextBlock  _totalBlock;
        private Button     _openChatBtn;
        private Button     _cancelBtn;

        public OccupancyAnalysis Analysis { get; }

        public OccupancyDialog(OccupancyAnalysis analysis)
        {
            Analysis = analysis;
            Title  = "Occupancy & Egress Analysis";
            Width  = 680; Height = 580;
            MinWidth = 580; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            ResizeMode = ResizeMode.CanResize;
            BuildUI();
        }

        private void BuildUI()
        {
            var outerScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel { Margin = new Thickness(20) };
            outerScroll.Content = root;

            // Header
            root.Children.Add(new TextBlock
            {
                Text = $"Occupant Load Analysis — {Analysis.ProjectName}",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 15, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            root.Children.Add(new TextBlock
            {
                Text = $"IBC 2021 Table 1004.5  ·  {Analysis.Records.Count} rooms  ·  {DateTime.Today:MMM d, yyyy}",
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
                FontSize = 11, Margin = new Thickness(0, 0, 0, 16)
            });

            // Table by level
            _tablePanel = new StackPanel();
            var byLevel = Analysis.Records.GroupBy(r => r.Level).OrderBy(g => g.Key);
            foreach (var level in byLevel)
            {
                // Level header
                _tablePanel.Children.Add(new TextBlock
                {
                    Text = level.Key,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 200, 255)),
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 4)
                });

                // Column headers
                _tablePanel.Children.Add(BuildHeaderRow());

                foreach (var r in level.OrderBy(r => r.RoomName))
                    _tablePanel.Children.Add(BuildDataRow(r));

                // Level total
                var levelLoad = level.Sum(r => r.OccupantLoad);
                var exits = levelLoad <= 0 ? 0 : levelLoad <= 499 ? 2 : levelLoad <= 999 ? 3 : 4;
                _tablePanel.Children.Add(BuildTotalRow(
                    $"Level total: {levelLoad} occupants  ·  Min exits required (IBC §1006): {exits}",
                    levelLoad));
            }
            root.Children.Add(_tablePanel);

            // Building total
            _totalBlock = new TextBlock
            {
                Text = $"Building total: {Analysis.TotalOccupants} occupants",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 4)
            };
            root.Children.Add(_totalBlock);

            root.Children.Add(new TextBlock
            {
                Text = "Room-to-IBC-category matching is based on room names. Rooms marked \"(default)\" defaulted to Business — verify these.",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 80)),
                FontSize = 10, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });

            _openChatBtn = new Button
            {
                Content = "Analyze Egress in Banana Chat", FontSize = 13,
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(40, 160, 80)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _openChatBtn.Click += (s, e) => { DialogResult = true; Close(); };
            root.Children.Add(_openChatBtn);

            _cancelBtn = new Button
            {
                Content = "Close", FontSize = 12,
                Padding = new Thickness(16, 6, 16, 6),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            _cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            root.Children.Add(_cancelBtn);

            Content = outerScroll;
        }

        private Grid BuildHeaderRow()
        {
            var g = MakeRowGrid();
            void Col(int i, string t) => AddCell(g, i, t, Color.FromRgb(120, 120, 120), 10, FontWeights.Normal);
            Col(0, "Room");
            Col(1, "Area (sf)");
            Col(2, "IBC Category");
            Col(3, "Factor");
            Col(4, "Load");
            g.Margin = new Thickness(0, 0, 0, 2);
            return g;
        }

        private Grid BuildDataRow(OccupancyRecord r)
        {
            var g = MakeRowGrid();
            bool isDefault = r.IbcCategory.Contains("default");
            bool noLoad    = r.LoadType == "N/A";

            var nameColor = isDefault
                ? Color.FromRgb(200, 170, 80)
                : noLoad ? Color.FromRgb(100, 100, 100) : Colors.White;

            AddCell(g, 0, r.RoomName,    nameColor, 11);
            AddCell(g, 1, r.AreaSqFt.ToString("N0"), Color.FromRgb(160, 160, 160), 11);
            AddCell(g, 2, r.IbcCategory, nameColor,  11);
            AddCell(g, 3, r.LoadType == "N/A" ? "—" : $"{r.LoadFactor:0}", Color.FromRgb(160, 160, 160), 11);
            AddCell(g, 4, r.OccupantLoad > 0 ? r.OccupantLoad.ToString() : "—",
                r.OccupantLoad > 0 ? Color.FromRgb(140, 210, 160) : Color.FromRgb(80, 80, 80), 11,
                r.OccupantLoad > 0 ? FontWeights.SemiBold : FontWeights.Normal);
            g.Margin = new Thickness(0, 0, 0, 1);
            return g;
        }

        private Border BuildTotalRow(string text, int load)
        {
            var b = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 4, 0, 0),
                Margin  = new Thickness(0, 2, 0, 4)
            };
            b.Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 200, 255)),
                FontSize = 11, FontWeight = FontWeights.SemiBold
            };
            return b;
        }

        private static Grid MakeRowGrid()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.0, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.4, GridUnitType.Star) });
            return g;
        }

        private static void AddCell(Grid g, int col, string text, Color color, double fontSize,
            FontWeight? weight = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = fontSize,
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(col == 0 ? 0 : 4, 0, 0, 0)
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }
    }
}
