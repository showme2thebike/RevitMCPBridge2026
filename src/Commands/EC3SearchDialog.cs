using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Commands
{
    public class EC3Epd
    {
        public string Id           { get; set; }
        public string Name         { get; set; }
        public string Manufacturer { get; set; }
        public string Gwp          { get; set; }
        public double? GwpNumeric  { get; set; }
        public string DeclaredUnit { get; set; }
        public string Category     { get; set; }
        public string Pct10        { get; set; }
        public string Pct50        { get; set; }
        public string ValidUntil   { get; set; }
    }

    public class EC3Result
    {
        public string       Query   { get; set; }
        public int          Total   { get; set; }
        public List<EC3Epd> Epds    { get; set; } = new List<EC3Epd>();

        public string FormatForPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"EC3 EPD Search: \"{Query}\"");
            sb.AppendLine($"Showing {Epds.Count} of {Total} results, sorted lowest GWP first");
            sb.AppendLine();

            for (int i = 0; i < Epds.Count; i++)
            {
                var e = Epds[i];
                sb.AppendLine($"{i+1}. {e.Name}");
                if (e.Manufacturer != null) sb.AppendLine($"   Manufacturer: {e.Manufacturer}");
                if (e.Gwp          != null) sb.AppendLine($"   GWP: {e.Gwp} per {e.DeclaredUnit}");
                if (e.Category     != null)
                {
                    sb.Append($"   Category: {e.Category}");
                    if (e.Pct10 != null && e.Pct50 != null)
                        sb.Append($" (industry median: {e.Pct50}, low 10th pct: {e.Pct10})");
                    sb.AppendLine();
                }
                if (e.ValidUntil   != null) sb.AppendLine($"   Valid until: {e.ValidUntil}");
            }
            return sb.ToString().TrimEnd();
        }
    }

    public class EC3SearchDialog : Window
    {
        private TextBox    _searchBox;
        private Button     _lookupBtn;
        private Button     _openChatBtn;
        private Button     _cancelBtn;
        private TextBlock  _statusBlock;
        private StackPanel _resultPanel;
        private StackPanel _resultList;
        private TextBlock  _summaryBlock;

        private readonly string _apiKey;
        private readonly string _railwayUrl;
        public  EC3Result Result { get; private set; }

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };

        public EC3SearchDialog(string apiKey, string railwayUrl)
        {
            _apiKey     = apiKey;
            _railwayUrl = railwayUrl;
            Title  = "EC3 EPD Search";
            Width  = 540; Height = 600;
            MinWidth = 460; MinHeight = 480;
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

            root.Children.Add(new TextBlock
            {
                Text = "Search Building Transparency EC3 for Environmental Product Declarations (EPDs) — results sorted by lowest embodied carbon",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Material or product type",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 12, Margin = new Thickness(0, 0, 0, 4)
            });
            _searchBox = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 4)
            };
            _searchBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) OnSearch(null, null); };
            root.Children.Add(_searchBox);
            root.Children.Add(new TextBlock
            {
                Text = "e.g. \"ready-mix concrete\", \"structural steel\", \"CLT\", \"mineral wool insulation\"",
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                FontSize = 11, Margin = new Thickness(0, 0, 0, 16)
            });

            _lookupBtn = new Button
            {
                Content = "Search EC3", FontSize = 13,
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(60, 120, 200)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _lookupBtn.Click += OnSearch;
            root.Children.Add(_lookupBtn);

            _statusBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };
            root.Children.Add(_statusBlock);

            _resultPanel = new StackPanel { Visibility = Visibility.Collapsed };

            _summaryBlock = new TextBlock
            {
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(140, 200, 140)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _resultPanel.Children.Add(_summaryBlock);

            _resultList = new StackPanel();
            _resultPanel.Children.Add(_resultList);

            _openChatBtn = new Button
            {
                Content = "Open in Banana Chat", FontSize = 13,
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(40, 160, 80)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            _openChatBtn.Click += (s, e) => { DialogResult = true; Close(); };
            _resultPanel.Children.Add(_openChatBtn);

            root.Children.Add(_resultPanel);

            _cancelBtn = new Button
            {
                Content = "Cancel", FontSize = 12,
                Padding = new Thickness(16, 6, 16, 6),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            _cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            root.Children.Add(_cancelBtn);

            Content = outerScroll;
        }

        private async void OnSearch(object sender, RoutedEventArgs e)
        {
            var query = _searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            _lookupBtn.IsEnabled = false;
            _resultPanel.Visibility = Visibility.Collapsed;
            _statusBlock.Text = "Searching EC3 database...";
            _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
            _statusBlock.Visibility = Visibility.Visible;

            try
            {
                var url = $"{_railwayUrl}/api/epd/search?q={Uri.EscapeDataString(query)}&page_size=20";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var resp = await _http.SendAsync(req);
                var raw  = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    string errMsg;
                    try   { errMsg = JObject.Parse(raw)["error"]?.ToString() ?? raw; }
                    catch { errMsg = $"HTTP {(int)resp.StatusCode}: {raw.Substring(0, Math.Min(200, raw.Length))}"; }
                    _statusBlock.Text = errMsg;
                    _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                    _lookupBtn.IsEnabled = true;
                    return;
                }

                JObject obj;
                try { obj = JObject.Parse(raw); }
                catch
                {
                    _statusBlock.Text = $"Unexpected response: {raw.Substring(0, Math.Min(200, raw.Length))}";
                    _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                    _lookupBtn.IsEnabled = true;
                    return;
                }

                var epdsArr = obj["epds"] as JArray ?? new JArray();
                var total   = obj["total"]?.ToObject<int>() ?? epdsArr.Count;

                Result = new EC3Result { Query = query, Total = total };
                foreach (JObject epd in epdsArr)
                {
                    Result.Epds.Add(new EC3Epd
                    {
                        Id           = epd["id"]?.ToString(),
                        Name         = epd["name"]?.ToString(),
                        Manufacturer = epd["manufacturer"]?.ToString(),
                        Gwp          = epd["gwp"]?.ToString(),
                        GwpNumeric   = epd["gwpNumeric"]?.ToObject<double?>(),
                        DeclaredUnit = epd["declaredUnit"]?.ToString(),
                        Category     = epd["category"]?.ToString(),
                        Pct10        = epd["pct10"]?.ToString(),
                        Pct50        = epd["pct50"]?.ToString(),
                        ValidUntil   = epd["validUntil"]?.ToString(),
                    });
                }

                _statusBlock.Visibility = Visibility.Collapsed;
                _summaryBlock.Text = $"{Result.Epds.Count} of {total} EPDs found for \"{query}\" — sorted lowest GWP first";

                _resultList.Children.Clear();
                foreach (var epd in Result.Epds)
                    _resultList.Children.Add(BuildEpdRow(epd));

                _resultPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _statusBlock.Text = $"Error: {ex.Message}";
                _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
            }
            finally { _lookupBtn.IsEnabled = true; }
        }

        private Border BuildEpdRow(EC3Epd epd)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 65)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 4)
            };
            var panel = new StackPanel();

            // Name + GWP on same line
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = epd.Name ?? "(unnamed)",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(nameBlock, 0);
            header.Children.Add(nameBlock);

            if (epd.Gwp != null)
            {
                var gwpBlock = new TextBlock
                {
                    Text = epd.Gwp,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 140)),
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(gwpBlock, 1);
                header.Children.Add(gwpBlock);
            }
            panel.Children.Add(header);

            // Sub-line: manufacturer + category
            var details = new StringBuilder();
            if (epd.Manufacturer != null) details.Append(epd.Manufacturer);
            if (epd.Category     != null) { if (details.Length > 0) details.Append(" · "); details.Append(epd.Category); }
            if (epd.DeclaredUnit != null) { if (details.Length > 0) details.Append(" · "); details.Append($"per {epd.DeclaredUnit}"); }

            if (details.Length > 0)
                panel.Children.Add(new TextBlock
                {
                    Text = details.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                    FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });

            // Benchmark line
            if (epd.Pct10 != null && epd.Pct50 != null)
                panel.Children.Add(new TextBlock
                {
                    Text = $"Industry: low {epd.Pct10} · median {epd.Pct50}",
                    Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
                    FontSize = 10, Margin = new Thickness(0, 1, 0, 0)
                });

            border.Child = panel;
            return border;
        }
    }
}
