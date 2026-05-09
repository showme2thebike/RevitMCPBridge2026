using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Commands
{
    public class ClimateResult
    {
        public string  Address         { get; set; }
        public string  MatchedAddress  { get; set; }
        public double? Lat             { get; set; }
        public double? Lng             { get; set; }
        public string  City            { get; set; }
        public string  State           { get; set; }
        public string  AshraeZone      { get; set; }
        public string  AshraeZoneName  { get; set; }
        public string  AshraeNote      { get; set; }
        public double? DesignHeatTemp  { get; set; }
        public double? DesignCoolTemp  { get; set; }
        public double? WinterMin       { get; set; }
        public double? SummerMax       { get; set; }
        public int?    AnnualHDD       { get; set; }
        public int?    AnnualCDD       { get; set; }
        public double? AnnualPrecipIn  { get; set; }
        public double? AvgWindMph      { get; set; }
        public string  PeakSolarMonth  { get; set; }
        public int?    DataYear        { get; set; }
        public string  Source          { get; set; }
        public string  Error           { get; set; }

        public string FormatForPrompt()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Address: {MatchedAddress ?? Address}");
            if (AshraeZone != null)
                sb.AppendLine($"ASHRAE 169 Climate Zone: {AshraeZone} — {AshraeZoneName}");
            if (DesignHeatTemp != null) sb.AppendLine($"Heating Design Temp (99%): {DesignHeatTemp}°F");
            if (DesignCoolTemp != null) sb.AppendLine($"Cooling Design Temp (1%):  {DesignCoolTemp}°F");
            if (WinterMin      != null) sb.AppendLine($"Record Winter Low ({DataYear}):  {WinterMin}°F");
            if (SummerMax      != null) sb.AppendLine($"Record Summer High ({DataYear}): {SummerMax}°F");
            if (AnnualHDD      != null) sb.AppendLine($"Annual HDD (65°F base): {AnnualHDD:N0}");
            if (AnnualCDD      != null) sb.AppendLine($"Annual CDD (65°F base): {AnnualCDD:N0}");
            if (AnnualPrecipIn != null) sb.AppendLine($"Annual Precipitation: {AnnualPrecipIn}\"");
            if (AvgWindMph     != null) sb.AppendLine($"Avg Max Wind Speed: {AvgWindMph} mph");
            if (PeakSolarMonth != null) sb.AppendLine($"Peak Solar Month: {PeakSolarMonth}");
            if (AshraeNote     != null) sb.AppendLine($"Note: {AshraeNote}");
            if (Source         != null) sb.AppendLine($"Source: {Source}");
            return sb.ToString().Trim();
        }
    }

    public class SiteClimateDialog : Window
    {
        private TextBox    _addressBox;
        private Button     _lookupBtn;
        private Button     _openChatBtn;
        private Button     _cancelBtn;
        private TextBox    _resultBlock;
        private TextBlock  _statusBlock;
        private StackPanel _resultPanel;

        private readonly string _apiKey;
        private readonly string _railwayUrl;
        public  ClimateResult Result { get; private set; }

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public SiteClimateDialog(string defaultAddress, string apiKey, string railwayUrl)
        {
            _apiKey     = apiKey;
            _railwayUrl = railwayUrl;
            Title  = "Site Climate Data";
            Width  = 480; Height = 540;
            MinWidth = 400; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            ResizeMode = ResizeMode.CanResize;
            BuildUI(defaultAddress);
        }

        private void BuildUI(string defaultAddress)
        {
            var root = new StackPanel { Margin = new Thickness(20) };

            root.Children.Add(new TextBlock
            {
                Text = "Pull ASHRAE climate zone and design conditions for a project address",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });
            root.Children.Add(new TextBlock
            {
                Text = "Address", Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 12, Margin = new Thickness(0, 0, 0, 4)
            });
            _addressBox = new TextBox
            {
                Text = defaultAddress ?? "", FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(_addressBox);
            root.Children.Add(new TextBlock
            {
                Text = "Include city and state — e.g. \"1234 Main St, Seattle, WA\"",
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                FontSize = 11, Margin = new Thickness(0, 0, 0, 16)
            });

            _lookupBtn = new Button
            {
                Content = "Look Up", FontSize = 13,
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(60, 120, 200)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _lookupBtn.Click += OnLookup;
            root.Children.Add(_lookupBtn);

            _statusBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 12, Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };
            root.Children.Add(_statusBlock);

            _resultPanel = new StackPanel { Visibility = Visibility.Collapsed };
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 16)
            };
            _resultBlock = new TextBox
            {
                IsReadOnly      = true,
                IsTabStop       = false,
                FontSize        = 12,
                Foreground      = new SolidColorBrush(Colors.White),
                FontFamily      = new FontFamily("Consolas"),
                TextWrapping    = TextWrapping.Wrap,
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(0),
            };
            border.Child = _resultBlock;
            _resultPanel.Children.Add(border);

            _openChatBtn = new Button
            {
                Content = "Open in Banana Chat", FontSize = 13,
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(40, 160, 80)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
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
            Content = root;
        }

        private async void OnLookup(object sender, RoutedEventArgs e)
        {
            var address = _addressBox.Text.Trim();
            if (string.IsNullOrEmpty(address)) return;

            _lookupBtn.IsEnabled = false;
            _resultPanel.Visibility = Visibility.Collapsed;
            _statusBlock.Text = "Fetching climate data (ERA5, 30-year normals)...";
            _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
            _statusBlock.Visibility = Visibility.Visible;

            try
            {
                var body = new JObject { ["address"] = address }.ToString(Newtonsoft.Json.Formatting.None);
                var req  = new HttpRequestMessage(HttpMethod.Post, $"{_railwayUrl}/api/climate/lookup");
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");

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

                Result = new ClimateResult
                {
                    Address        = address,
                    MatchedAddress = obj["matchedAddress"]?.ToString(),
                    City           = obj["city"]?.ToString(),
                    State          = obj["state"]?.ToString(),
                    AshraeZone     = obj["ashraeZone"]?.ToString(),
                    AshraeZoneName = obj["ashraeZoneName"]?.ToString(),
                    AshraeNote     = obj["ashraeNote"]?.ToString(),
                    DesignHeatTemp = obj["designHeatTemp"]?.ToObject<double?>(),
                    DesignCoolTemp = obj["designCoolTemp"]?.ToObject<double?>(),
                    WinterMin      = obj["winterMin"]?.ToObject<double?>(),
                    SummerMax      = obj["summerMax"]?.ToObject<double?>(),
                    AnnualHDD      = obj["annualHDD"]?.ToObject<int?>(),
                    AnnualCDD      = obj["annualCDD"]?.ToObject<int?>(),
                    AnnualPrecipIn = obj["annualPrecipIn"]?.ToObject<double?>(),
                    AvgWindMph     = obj["avgWindMph"]?.ToObject<double?>(),
                    PeakSolarMonth = obj["peakSolarMonth"]?.ToString(),
                    DataYear       = obj["dataYear"]?.ToObject<int?>(),
                    Source         = obj["source"]?.ToString(),
                };

                _statusBlock.Visibility = Visibility.Collapsed;
                _resultBlock.Text = Result.FormatForPrompt();
                _resultPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _statusBlock.Text = $"Error: {ex.Message}";
                _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
            }
            finally { _lookupBtn.IsEnabled = true; }
        }
    }
}
