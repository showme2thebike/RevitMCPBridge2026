using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Commands
{
    public class PermitHistoryDialog : Window
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

        public PermitHistoryDialog(string defaultAddress, string apiKey, string railwayUrl)
        {
            _apiKey     = apiKey;
            _railwayUrl = railwayUrl;
            Title  = "Permit History Lookup";
            Width  = 520; Height = 520;
            MinWidth = 420; MinHeight = 400;
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
                Text = "Look up recent building permit history for a project address",
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
                FontSize = 11, Margin = new Thickness(0, 0, 0, 4)
            });
            root.Children.Add(new TextBlock
            {
                Text = "Permit data available: Seattle, NYC, Chicago, LA, SF, Austin, Denver, DC, Portland, Miami, Philadelphia, Nashville, Minneapolis",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 100)),
                FontSize = 10, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
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
                Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 16),
                MaxHeight = 240
            };
            _resultBlock = new TextBox
            {
                IsReadOnly      = true,
                IsTabStop       = false,
                FontSize        = 11,
                Foreground      = new SolidColorBrush(Colors.White),
                FontFamily      = new FontFamily("Consolas"),
                TextWrapping    = TextWrapping.Wrap,
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(0),
                MaxHeight       = 216,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
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
            _statusBlock.Text = "Fetching permit history...";
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
                    ParcelId       = obj["parcelId"]?.ToString(),
                    PermitHistory  = obj["permitHistory"] as JArray,
                    Source         = obj["source"]?.ToString(),
                };

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Address: {Result.MatchedAddress ?? Result.Address}");
                sb.AppendLine();

                if (Result.PermitHistory != null && Result.PermitHistory.Count > 0)
                {
                    sb.AppendLine($"Recent Permits ({Result.PermitHistory.Count} found):");
                    foreach (JObject p in Result.PermitHistory)
                    {
                        var date = p["applicationDate"]?.ToString() ?? p["date"]?.ToString() ?? "—";
                        var type = p["type"]?.ToString() ?? "—";
                        var desc = p["description"]?.ToString() ?? "";
                        var stat = p["status"]?.ToString() ?? "";
                        sb.AppendLine($"  {date}  {type}");
                        if (!string.IsNullOrEmpty(desc)) sb.AppendLine($"  {desc}");
                        if (!string.IsNullOrEmpty(stat)) sb.AppendLine($"  Status: {stat}");
                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("No recent permit records found for this address.");
                    sb.AppendLine("This may mean no recent permits, or that the city isn't");
                    sb.AppendLine("yet covered in BIM Monkey's permit database.");
                }

                _statusBlock.Visibility = Visibility.Collapsed;
                _resultBlock.Text = sb.ToString().Trim();
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
