using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Scanner.WPF.Services;

namespace Scanner.WPF
{
    public sealed class OmsShippingWindow : Window
    {
        private readonly OmsShippingService _api = new OmsShippingService();
        private readonly Dictionary<string, TextBox> _fields = new Dictionary<string, TextBox>();
        private readonly PasswordBox _password = new PasswordBox();
        private readonly ComboBox _products = new ComboBox { DisplayMemberPath = "name" };
        private readonly ComboBox _shippers = new ComboBox { DisplayMemberPath = "shipperAddress1" };
        private readonly StackPanel _form = new StackPanel { Margin = new Thickness(12) };
        private readonly Image _preview = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(12) };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
        private readonly Button _pay = new Button { Content = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow25"), IsEnabled = false, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8) };
        private JObject _payload, _quote;
        private JToken _unit;
        private BitmapSource _image;
        private string _pdf;
        private bool _busy;
        private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scanner", "OMS");

        public OmsShippingWindow(BluettiLabelRow row = null)
        {
            Title = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow24"); Width = 1200; Height = 850; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(IconProperty, "Logo");
            var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) }); layout.ColumnDefinitions.Add(new ColumnDefinition());
            layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var scroll = new ScrollViewer { Content = _form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; layout.Children.Add(scroll);
            Grid.SetColumn(_preview, 1); layout.Children.Add(_preview); Grid.SetRow(_status, 1); Grid.SetColumnSpan(_status, 2); layout.Children.Add(_status); Content = layout;
            Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow26")); Field("username", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow27")); Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow28")); _form.Children.Add(_password);
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow29"), async () =>
            {
                _pay.IsEnabled = false; _payload = _quote = null;
                await _api.LoginAsync(Value("username"), _password.Password, CancellationToken.None); _password.Clear();
                var products = await _api.ProductsAsync(CancellationToken.None); var shippers = await _api.ShippersAsync(CancellationToken.None); _unit = await _api.InchPoundUnitAsync(CancellationToken.None);
                _products.ItemsSource = products.OfType<JObject>().ToArray();
                _products.SelectedItem = products.OfType<JObject>().FirstOrDefault(p => ((string)p["name"] ?? "").Replace(" ", "").Equals("FedEx-美东-NJ", StringComparison.OrdinalIgnoreCase));
                _shippers.ItemsSource = shippers.OfType<JObject>().ToArray(); if (shippers.Count > 0) _shippers.SelectedIndex = 0;
                _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow30");
            });
            Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow31")); _form.Children.Add(_products); Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow32")); _form.Children.Add(_shippers);
            var sender = new TextBlock { TextWrapping = TextWrapping.Wrap }; _form.Children.Add(sender);
            _shippers.SelectionChanged += (_, _) => { var s = _shippers.SelectedItem as JObject; sender.Text = s == null ? "" : string.Join(" / ", new[] { "shipperName", "shipperCompanyName", "shipperAddress1", "shipperCity", "shipperStateOrProvince", "shipperPostCode", "shipperPhone" }.Select(k => (string)s[k])); InvalidateQuote(); };
            _products.SelectionChanged += (_, _) => InvalidateQuote();
            Field("customer", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow33"), row?.WorkOrder); Field("name", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow34"), row?.Name); Field("email", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow35"), row?.Email); Field("phone", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow36"), row?.Phone); Field("address", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow37"), row?.Address);
            Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow38"));
            Button("查询并应用 SKU 尺寸 / 重量", () =>
            {
                var lookup = new ShippingSkuWindow(row?.Model ?? "") { Owner = this };
                if (lookup.ShowDialog() == true)
                {
                    var sku = lookup.SelectedSku;
                    _fields["length"].Text = sku.LengthInches?.ToString(CultureInfo.InvariantCulture) ?? "";
                    _fields["width"].Text = sku.WidthInches?.ToString(CultureInfo.InvariantCulture) ?? "";
                    _fields["height"].Text = sku.HeightInches?.ToString(CultureInfo.InvariantCulture) ?? "";
                    _fields["weight"].Text = sku.WeightPounds?.ToString(CultureInfo.InvariantCulture) ?? "";
                    _status.Text = $"已应用 SKU {sku.Sku} 的包装尺寸和重量（IN / LB）。";
                }
                return Task.CompletedTask;
            });
            Field("length", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow39")); Field("width", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow40")); Field("height", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow41")); Field("weight", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow42"));
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow43"), async () =>
            {
                _pay.IsEnabled = false; _quote = null;
                var shipper = _shippers.SelectedItem as JObject ?? throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow44"));
                var product = _products.SelectedItem as JObject ?? throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow45"));
                decimal Number(string key) => decimal.TryParse(Value(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : throw new ArgumentException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow46"));
                var shipment = new OmsShipment { CustomerNo = Value("customer"), Name = Value("name"), Email = Value("email"), Phone = Value("phone"), Address = Value("address"), Length = Number("length"), Width = Number("width"), Height = Number("height"), Weight = Number("weight") };
                _payload = shipment.Build(shipper, (string)product["id"], _unit);
                _quote = await _api.QuoteAsync(_payload, CancellationToken.None);
                if (_quote["orderFeeDetail"]?["settleTotalPrice"] == null || _quote["orderFeeDetail"]?["settleCurrency"] == null) throw new InvalidDataException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow47"));
                _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow48") + _quote["orderFeeDetail"]["settleTotalPrice"] + " " + _quote["orderFeeDetail"]["settleCurrency"] + Scanner.WPF.Helpers.UiText.Get("WpfNewWindow49"); _pay.IsEnabled = true;
            });
            _pay.Click += async (_, _) =>
            {
                if (_payload == null || _quote == null) return;
                var fee = _quote["orderFeeDetail"];
                string message = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow50") + (string)_payload["customerNo"] + Scanner.WPF.Helpers.UiText.Get("WpfNewWindow51") + fee["settleTotalPrice"] + " " + fee["settleCurrency"] + Scanner.WPF.Helpers.UiText.Get("WpfNewWindow52");
                if (MessageBox.Show(this, message, Scanner.WPF.Helpers.UiText.Get("WpfNewWindow53"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                _pay.IsEnabled = false;
                await Run(async () =>
                {
                    _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow54");
                    var result = await _api.PayOnceAsync(_payload, Path.Combine(_root, "Orders"), CancellationToken.None);
                    string id = (string)result["id"] ?? throw new InvalidDataException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow55"));
                    _fields["orderId"].Text = id;
                    _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow56") + id + Scanner.WPF.Helpers.UiText.Get("WpfNewWindow57");
                });
            }; _form.Children.Add(_pay);
            Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow58")); Field("orderId", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow59"));
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow60"), async () => { var uri = await _api.GetLabelAsync(Value("orderId"), CancellationToken.None); await Download(uri); });
            Field("url", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow61")); Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow62"), async () => await Download(new Uri(Value("url"), UriKind.Absolute)));
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow63"), async () => { var dialog = new OpenFileDialog { Filter = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow64") + "|*.pdf" }; if (dialog.ShowDialog(this) == true) { _pdf = dialog.FileName; await Preview(); } });
            Label(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow65")); Field("rma", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow66"), row?.Rma); Field("model", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow67"), row?.Model); Field("serial", Scanner.WPF.Helpers.UiText.Get("WpfNewWindow68"), row?.Sn);
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow69"), Preview);
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow70"), () =>
            {
                if (_image == null) throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow71"));
                var dialog = new SaveFileDialog { Filter = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow64") + "|*.pdf|" + Scanner.WPF.Helpers.UiText.Get("WpfNewWindow72") + "|*.png", FileName = "OMS-label" };
                if (dialog.ShowDialog(this) == true) { if (dialog.FilterIndex == 1) OmsLabelDocument.SavePdf(_image, dialog.FileName); else OmsLabelDocument.SavePng(_image, dialog.FileName); _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow73"); }
                return Task.CompletedTask;
            });
            Button(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow74"), () => { if (_image == null) throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow71")); OmsLabelDocument.Print(_image); return Task.CompletedTask; });
            Closing += (_, e) => { if (_busy) { e.Cancel = true; _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow75"); } }; Closed += (_, _) => _api.Dispose();
            _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow76");
        }
        private void Label(string text) => _form.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 3) });
        private void Field(string key, string text, string initial = null)
        {
            Label(text); var box = new TextBox { Text = initial ?? "", Padding = new Thickness(4) }; _fields.Add(key, box); _form.Children.Add(box);
            box.TextChanged += (_, _) => { if (new[] { "customer", "name", "email", "phone", "address", "length", "width", "height", "weight" }.Contains(key)) InvalidateQuote(); if (new[] { "rma", "model", "serial" }.Contains(key)) { _image = null; _preview.Source = null; } };
        }
        private string Value(string key) => _fields[key].Text.Trim();
        private void InvalidateQuote() { _quote = null; _payload = null; _pay.IsEnabled = false; }
        private void Button(string text, Func<Task> action) { var button = new Button { Content = text, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8) }; button.Click += async (_, _) => await Run(action); _form.Children.Add(button); }
        private async Task Run(Func<Task> action)
        {
            if (_busy) return; _busy = true; _form.IsEnabled = false;
            try { await action(); } catch (Exception ex) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow77") + ex.Message; }
            finally { _form.IsEnabled = true; _busy = false; }
        }
        private async Task Download(Uri uri)
        {
            _image = null; _preview.Source = null; _pdf = null;
            Directory.CreateDirectory(Path.Combine(_root, "Labels"));
            var path = Path.Combine(_root, "Labels", Guid.NewGuid().ToString("N") + ".pdf");
            await _api.DownloadLabelAsync(uri, path, CancellationToken.None); _pdf = path; await Preview(); _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow78");
        }
        private async Task Preview()
        {
            if (_pdf == null) throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow79"));
            _image = null; _preview.Source = null;
            _image = await OmsLabelDocument.RenderAsync(_pdf, Value("rma"), Value("model"), Value("serial")); _preview.Source = _image;
        }
    }
}
