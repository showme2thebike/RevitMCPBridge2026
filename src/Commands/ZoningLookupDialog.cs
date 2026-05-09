using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Commands
{
    public class ParcelResult
    {
        public string   Address          { get; set; }
        public string   MatchedAddress   { get; set; }
        public double?  Lat              { get; set; }
        public double?  Lng              { get; set; }
        public string   ParcelId         { get; set; }
        public int?     LotArea          { get; set; }
        public double?  LotAreaAcres     { get; set; }
        public string   Owner            { get; set; }
        public int?     YearBuilt        { get; set; }
        public long?    AssessedValue    { get; set; }
        public int?     BuildingArea     { get; set; }
        public string   PropType         { get; set; }
        public string   Zoning           { get; set; }
        public string   ZoningDescription { get; set; }
        public string   ZoningCategory   { get; set; }
        public JObject  Setbacks         { get; set; }
        public double?  Far              { get; set; }
        public double?  MaxHeight        { get; set; }
        public double?  LotCoverage      { get; set; }
        public JArray   PermittedUses    { get; set; }
        public JArray   ConditionalUses  { get; set; }
        public JArray   Overlays         { get; set; }
        public JArray   PermitHistory    { get; set; }
        public string   Source           { get; set; }
        public string   Coverage         { get; set; }
        public JArray   Notes            { get; set; }
        public string   Error            { get; set; }

        public string FormatForPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Address: {MatchedAddress ?? Address}");
            if (ParcelId    != null) sb.AppendLine($"Parcel ID: {ParcelId}");
            if (LotArea     != null) sb.AppendLine($"Lot Area: {LotArea:N0} sq ft ({LotAreaAcres:0.000} acres)");
            if (Owner       != null) sb.AppendLine($"Owner: {Owner}");
            if (YearBuilt   != null) sb.AppendLine($"Year Built: {YearBuilt}");
            if (AssessedValue != null) sb.AppendLine($"Assessed Value: ${AssessedValue:N0}");
            if (BuildingArea != null) sb.AppendLine($"Building Footprint: {BuildingArea:N0} sq ft");
            if (PropType    != null) sb.AppendLine($"Property Type: {PropType}");
            if (Zoning      != null) sb.AppendLine($"Zoning: {Zoning}{(ZoningDescription != null ? $" — {ZoningDescription}" : "")}{(ZoningCategory != null ? $" ({ZoningCategory})" : "")}");

            if (Setbacks != null)
            {
                sb.AppendLine("Setbacks:");
                if (Setbacks["front"]        != null) sb.AppendLine($"  Front:         {Setbacks["front"]}'");
                if (Setbacks["rear"]         != null) sb.AppendLine($"  Rear:          {Setbacks["rear"]}'");
                if (Setbacks["sideInterior"] != null) sb.AppendLine($"  Side (int):    {Setbacks["sideInterior"]}'");
                if (Setbacks["sideStreet"]   != null) sb.AppendLine($"  Side (street): {Setbacks["sideStreet"]}'");
                if (Setbacks["notes"]        != null) sb.AppendLine($"  Note: {Setbacks["notes"]}");
            }

            if (Far         != null) sb.AppendLine($"Max FAR: {Far}");
            if (MaxHeight   != null) sb.AppendLine($"Max Height: {MaxHeight}'");
            if (LotCoverage != null) sb.AppendLine($"Max Lot Coverage: {LotCoverage}%");

            if (PermittedUses   != null && PermittedUses.Count   > 0) sb.AppendLine($"Permitted Uses: {string.Join(", ", PermittedUses)}");
            if (ConditionalUses != null && ConditionalUses.Count > 0) sb.AppendLine($"Conditional Uses: {string.Join(", ", ConditionalUses)}");
            if (Overlays        != null && Overlays.Count        > 0) sb.AppendLine($"Overlay Districts: {string.Join(", ", Overlays)}");

            if (PermitHistory != null && PermitHistory.Count > 0)
            {
                sb.AppendLine($"Recent Permits ({PermitHistory.Count}):");
                foreach (JObject p in PermitHistory)
                    sb.AppendLine($"  - {p["applicationDate"]} {p["type"]}: {p["description"]} [{p["status"]}]");
            }

            if (Notes != null && Notes.Count > 0)
                foreach (var n in Notes)
                    sb.AppendLine($"Note: {n}");

            if (Source != null) sb.AppendLine($"Source: {Source}");
            return sb.ToString().Trim();
        }
    }

    public class ZoningLookupDialog : Window
    {
        private TextBox     _addressBox;
        private Button      _lookupBtn;
        private Button      _openChatBtn;
        private Button      _cancelBtn;
        private TextBox     _resultBlock;
        private TextBlock   _statusBlock;
        private StackPanel  _resultPanel;

        private readonly string _apiKey;
        private readonly string _railwayUrl;
        public  ParcelResult Result { get; private set; }

        private static readonly HttpClient _http = new HttpClient();

        public ZoningLookupDialog(string defaultAddress, string apiKey, string railwayUrl)
        {
            _apiKey     = apiKey;
            _railwayUrl = railwayUrl;

            Title  = "Zoning & Parcel Lookup";
            Width  = 480;
            Height = 520;
            MinWidth  = 400;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            ResizeMode = ResizeMode.CanResize;

            BuildUI(defaultAddress);
        }

        private void BuildUI(string defaultAddress)
        {
            var root = new StackPanel { Margin = new Thickness(20) };

            // Title
            root.Children.Add(new TextBlock
            {
                Text       = "Look up parcel data for a project address",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize   = 14,
                Margin     = new Thickness(0, 0, 0, 16)
            });

            // Address label + input
            root.Children.Add(new TextBlock
            {
                Text       = "Address",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize   = 12,
                Margin     = new Thickness(0, 0, 0, 4)
            });

            _addressBox = new TextBox
            {
                Text              = defaultAddress ?? "",
                FontSize          = 13,
                Padding           = new Thickness(10, 8, 10, 8),
                Background        = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                Foreground        = new SolidColorBrush(Colors.White),
                BorderBrush       = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness   = new Thickness(1),
                Margin            = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(_addressBox);

            root.Children.Add(new TextBlock
            {
                Text       = "Include city and state — e.g. \"1234 Main St, Seattle, WA\"",
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                FontSize   = 11,
                Margin     = new Thickness(0, 0, 0, 16)
            });

            // Look Up button
            _lookupBtn = new Button
            {
                Content         = "Look Up",
                FontSize        = 13,
                Padding         = new Thickness(20, 8, 20, 8),
                Background      = new SolidColorBrush(Color.FromRgb(60, 120, 200)),
                Foreground      = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin          = new Thickness(0, 0, 0, 16)
            };
            _lookupBtn.Click += OnLookup;
            root.Children.Add(_lookupBtn);

            // Status
            _statusBlock = new TextBlock
            {
                Foreground  = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize    = 12,
                Margin      = new Thickness(0, 0, 0, 8),
                Visibility  = Visibility.Collapsed
            };
            root.Children.Add(_statusBlock);

            // Result panel
            _resultPanel = new StackPanel { Visibility = Visibility.Collapsed };

            var resultBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(12),
                Margin          = new Thickness(0, 0, 0, 16)
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
            resultBorder.Child = _resultBlock;
            _resultPanel.Children.Add(resultBorder);

            // Open in Banana Chat button
            _openChatBtn = new Button
            {
                Content         = "Open in Banana Chat",
                FontSize        = 13,
                Padding         = new Thickness(20, 8, 20, 8),
                Background      = new SolidColorBrush(Color.FromRgb(40, 160, 80)),
                Foreground      = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _openChatBtn.Click += (s, e) => { DialogResult = true; Close(); };
            _resultPanel.Children.Add(_openChatBtn);

            root.Children.Add(_resultPanel);

            // Cancel
            _cancelBtn = new Button
            {
                Content         = "Cancel",
                FontSize        = 12,
                Padding         = new Thickness(16, 6, 16, 6),
                Background      = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground      = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin          = new Thickness(0, 12, 0, 0)
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
            _statusBlock.Text       = "Looking up parcel data...";
            _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
            _statusBlock.Visibility = Visibility.Visible;

            try
            {
                var body    = new JObject { ["address"] = address }.ToString(Newtonsoft.Json.Formatting.None);
                var req     = new HttpRequestMessage(HttpMethod.Post, $"{_railwayUrl}/api/parcel/lookup");
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req);
                var raw  = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    string errMsg;
                    try   { errMsg = JObject.Parse(raw)["error"]?.ToString() ?? raw; }
                    catch { errMsg = $"HTTP {(int)resp.StatusCode}: {raw.Substring(0, Math.Min(200, raw.Length))}"; }
                    _statusBlock.Text       = errMsg;
                    _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                    _lookupBtn.IsEnabled    = true;
                    return;
                }

                JObject obj;
                try { obj = JObject.Parse(raw); }
                catch
                {
                    _statusBlock.Text       = $"Server returned unexpected response: {raw.Substring(0, Math.Min(200, raw.Length))}";
                    _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                    _lookupBtn.IsEnabled    = true;
                    return;
                }

                Result = new ParcelResult
                {
                    Address          = address,
                    MatchedAddress   = obj["matchedAddress"]?.ToString(),
                    Lat              = obj["lat"]?.ToObject<double?>(),
                    Lng              = obj["lng"]?.ToObject<double?>(),
                    ParcelId         = obj["parcelId"]?.ToString(),
                    LotArea          = obj["lotArea"]?.ToObject<int?>(),
                    LotAreaAcres     = obj["lotAreaAcres"]?.ToObject<double?>(),
                    Owner            = obj["owner"]?.ToString(),
                    YearBuilt        = obj["yearBuilt"]?.ToObject<int?>(),
                    AssessedValue    = obj["assessedValue"]?.ToObject<long?>(),
                    BuildingArea     = obj["buildingArea"]?.ToObject<int?>(),
                    PropType         = obj["propType"]?.ToString(),
                    Zoning           = obj["zoning"]?.ToString(),
                    ZoningDescription = obj["zoningDescription"]?.ToString(),
                    ZoningCategory   = obj["zoningCategory"]?.ToString(),
                    Setbacks         = obj["setbacks"] as JObject,
                    Far              = obj["far"]?.ToObject<double?>(),
                    MaxHeight        = obj["maxHeight"]?.ToObject<double?>(),
                    LotCoverage      = obj["lotCoverage"]?.ToObject<double?>(),
                    PermittedUses    = obj["permittedUses"]   as JArray,
                    ConditionalUses  = obj["conditionalUses"] as JArray,
                    Overlays         = obj["overlays"]        as JArray,
                    PermitHistory    = obj["permitHistory"]   as JArray,
                    Notes            = obj["notes"]           as JArray,
                    Source           = obj["coverage"]?.ToString() == "address_only"
                                         ? "Geocoded — no parcel data available for this location yet"
                                         : obj["source"]?.ToString(),
                    Coverage         = obj["coverage"]?.ToString()
                };

                // Zoning dialog shows code-relevant data only
                // (Parcel ID, lot area, owner, permits → those live in their own dialogs)
                var disp = new System.Text.StringBuilder();
                disp.AppendLine($"Address:  {Result.MatchedAddress ?? Result.Address}");
                if (Result.Zoning != null)
                {
                    var z = Result.Zoning;
                    if (Result.ZoningDescription != null) z += $" — {Result.ZoningDescription}";
                    if (Result.ZoningCategory    != null) z += $" ({Result.ZoningCategory})";
                    disp.AppendLine($"Zoning:   {z}");
                }
                if (Result.Setbacks != null)
                {
                    disp.AppendLine("Setbacks:");
                    if (Result.Setbacks["front"]        != null) disp.AppendLine($"  Front:      {Result.Setbacks["front"]}'");
                    if (Result.Setbacks["rear"]         != null) disp.AppendLine($"  Rear:       {Result.Setbacks["rear"]}'");
                    if (Result.Setbacks["sideInterior"] != null) disp.AppendLine($"  Side (int): {Result.Setbacks["sideInterior"]}'");
                    if (Result.Setbacks["sideStreet"]   != null) disp.AppendLine($"  Side (str): {Result.Setbacks["sideStreet"]}'");
                }
                if (Result.Far        != null) disp.AppendLine($"Max FAR:  {Result.Far}");
                if (Result.MaxHeight  != null) disp.AppendLine($"Height:   {Result.MaxHeight}'");
                if (Result.LotCoverage != null) disp.AppendLine($"Lot Cov:  {Result.LotCoverage}%");
                if (Result.PermittedUses   != null && Result.PermittedUses.Count   > 0) disp.AppendLine($"Permitted: {string.Join(", ", Result.PermittedUses)}");
                if (Result.ConditionalUses != null && Result.ConditionalUses.Count > 0) disp.AppendLine($"Conditional: {string.Join(", ", Result.ConditionalUses)}");
                if (Result.Overlays        != null && Result.Overlays.Count        > 0) disp.AppendLine($"Overlays: {string.Join(", ", Result.Overlays)}");
                if (Result.Notes != null)
                    foreach (var n in Result.Notes) disp.AppendLine($"Note: {n}");
                if (Result.Source != null) disp.AppendLine($"Source:   {Result.Source}");

                _statusBlock.Visibility = Visibility.Collapsed;
                _resultBlock.Text       = disp.ToString().Trim();
                _resultPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _statusBlock.Text       = $"Error: {ex.Message}";
                _statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
            }
            finally
            {
                _lookupBtn.IsEnabled = true;
            }
        }
    }
}
