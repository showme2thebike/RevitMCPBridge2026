using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Commands
{
    public class ParcelDataDialog : Window
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
        public  ParcelResult Result { get; private set; }

        private static readonly HttpClient _http = new HttpClient();

        public ParcelDataDialog(string defaultAddress, string apiKey, string railwayUrl)
        {
            _apiKey     = apiKey;
            _railwayUrl = railwayUrl;
            Title  = "Parcel Data Lookup";
            Width  = 480; Height = 460;
            MinWidth = 400; MinHeight = 360;
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
                Text = "Look up parcel ID and lot area for a project address",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14, Margin = new Thickness(0, 0, 0, 16)
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
            _statusBlock.Text = "Looking up parcel data...";
            _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
            _statusBlock.Visibility = Visibility.Visible;

            try
            {
                var body = new JObject { ["address"] = address }.ToString(Newtonsoft.Json.Formatting.None);
                var req  = new HttpRequestMessage(HttpMethod.Post, $"{_railwayUrl}/api/parcel/lookup");
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

                Result = new ParcelResult
                {
                    Address        = address,
                    MatchedAddress = obj["matchedAddress"]?.ToString(),
                    Lat            = obj["lat"]?.ToObject<double?>(),
                    Lng            = obj["lng"]?.ToObject<double?>(),
                    ParcelId       = obj["parcelId"]?.ToString(),
                    LotArea        = obj["lotArea"]?.ToObject<int?>(),
                    LotAreaAcres   = obj["lotAreaAcres"]?.ToObject<double?>(),
                    Owner          = obj["owner"]?.ToString(),
                    YearBuilt      = obj["yearBuilt"]?.ToObject<int?>(),
                    AssessedValue  = obj["assessedValue"]?.ToObject<long?>(),
                    BuildingArea   = obj["buildingArea"]?.ToObject<int?>(),
                    PropType       = obj["propType"]?.ToString(),
                    Source         = obj["source"]?.ToString(),
                };

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Address:    {Result.MatchedAddress ?? Result.Address}");
                if (Result.ParcelId     != null) sb.AppendLine($"Parcel ID:  {Result.ParcelId}");
                if (Result.LotArea      != null) sb.AppendLine($"Lot Area:   {Result.LotArea:N0} sq ft  ({Result.LotAreaAcres:0.000} acres)");
                if (Result.BuildingArea != null) sb.AppendLine($"Bldg Area:  {Result.BuildingArea:N0} sq ft (footprint)");
                if (Result.Owner        != null) sb.AppendLine($"Owner:      {Result.Owner}");
                if (Result.YearBuilt    != null) sb.AppendLine($"Year Built: {Result.YearBuilt}");
                if (Result.AssessedValue != null) sb.AppendLine($"Assessed:   ${Result.AssessedValue:N0}");
                if (Result.PropType     != null) sb.AppendLine($"Prop Type:  {Result.PropType}");
                if (Result.Lat          != null) sb.AppendLine($"Coords:     {Result.Lat:0.0000}, {Result.Lng:0.0000}");
                if (Result.Source       != null) sb.AppendLine($"Source:     {Result.Source}");

                sb.AppendLine();
                sb.AppendLine("Parcel boundary polygon (GeoJSON footprint) — roadmap Q4 2026");

                _statusBlock.Visibility = Visibility.Collapsed;
                _resultBlock.Text = sb.ToString().Trim();
                _resultPanel.Visibility = Visibility.Visible;
                _resultBlock.Focus();
                _resultBlock.SelectAll();
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
