using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Newtonsoft.Json.Linq;
using Scanner.WPF.Services;

namespace Scanner.WPF
{
    public partial class ShipmentTrackingWindow : Window
    {
        private readonly ObservableCollection<ShipmentItem> shipments = new ObservableCollection<ShipmentItem>();
        private readonly FedExTrackingService service = new FedExTrackingService();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly MemoryLayer positions = new MemoryLayer { Name = Scanner.WPF.Helpers.UiText.Get("WpfSupplement12"), Style = new SymbolStyle { Fill = new Brush(Color.FromString("#5B2B82")), SymbolScale = 0.9 } };
        private bool busy;
        public ShipmentTrackingWindow()
        {
            InitializeComponent();
            ShipmentList.ItemsSource = shipments;
            TrackingMap.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
            TrackingMap.Map.Layers.Add(positions);
            Closed += (_, __) => { lifetime.Cancel(); service.Dispose(); TrackingMap.Dispose(); };
        }
        private void Window_Loaded(object sender, RoutedEventArgs e) => TrackingInput.Focus();
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            var number = Regex.Replace(TrackingInput.Text ?? "", @"\s+", "");
            if (!Regex.IsMatch(number, @"^\d{12,22}$"))
            {
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfSupplement13");
                return;
            }
            var item = shipments.FirstOrDefault(s => s.Number == number);
            if (item == null) { item = new ShipmentItem { Number = number }; shipments.Add(item); }
            ShipmentList.SelectedItem = item;
            busy = true;
            ConfirmButton.IsEnabled = false;
            item.Status = Scanner.WPF.Helpers.UiText.Get("WpfSupplement14");
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfSupplement15");
            try
            {
                var result = await service.TrackAsync(number, lifetime.Token);
                item.Result = result;
                item.QueriedAt = DateTime.Now;
                item.Status = result.SelectToken("latestStatusDetail.description")?.ToString() ?? Scanner.WPF.Helpers.UiText.Get("WpfSupplement16");
            }
            catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) item.Status = Scanner.WPF.Helpers.UiText.Get("WpfSupplement17"); }
            catch (Exception ex) { item.Status = ex is InvalidOperationException ? ex.Message : Scanner.WPF.Helpers.UiText.Get("WpfSupplement18"); }
            finally
            {
                if (!lifetime.IsCancellationRequested)
                {
                    busy = false;
                    ConfirmButton.IsEnabled = true;
                    ShowSelected();
                }
            }
        }
        private void TrackingInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { e.Handled = true; Confirm_Click(sender, e); }
        }
        private void ShipmentList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowSelected();
        private void ShowSelected()
        {
            if (ShipmentList.SelectedItem is not ShipmentItem item) return;
            var events = item.Result?["scanEvents"] as JArray ?? new JArray();
            EventList.ItemsSource = events.Select(scan => string.Join("  ·  ", new[]
            {
                scan["date"]?.ToString(), scan["eventDescription"]?.ToString(),
                string.Join(", ", new[] { scan.SelectToken("scanLocation.city")?.ToString(), scan.SelectToken("scanLocation.stateOrProvinceCode")?.ToString(), scan.SelectToken("scanLocation.countryCode")?.ToString() }.Where(s => !string.IsNullOrWhiteSpace(s)))
            }.Where(s => !string.IsNullOrWhiteSpace(s)))).ToList();
            var points = events.Select(scan => scan["scanLocation"]).Where(location => location != null)
                .Select(location => new { Lat = Parse(location["latitude"]), Lon = Parse(location["longitude"]) })
                .Where(p => p.Lat.HasValue && p.Lon.HasValue && Math.Abs(p.Lat.Value) <= 85 && Math.Abs(p.Lon.Value) <= 180)
                .Select(p => { var xy = SphericalMercator.FromLonLat(p.Lon.Value, p.Lat.Value); return new PointFeature(new MPoint(xy.x, xy.y)); }).ToList();
            positions.Features = points;
            positions.DataHasChanged();
            if (points.Count > 0) TrackingMap.Map.Navigator.CenterOn(points[0].Point);
            TrackingMap.Refresh();
            StatusText.Text = item.Number + "  ·  " + item.Status +
                (item.Result == null ? "" : points.Count == 0 ? Scanner.WPF.Helpers.UiText.Get("WpfSupplement19") : Scanner.WPF.Helpers.UiText.Get("WpfSupplement20")) +
                (item.Result == null ? "" : Scanner.WPF.Helpers.UiText.Get("WpfSupplement21") + item.QueriedAt.ToString("yyyy-MM-dd HH:mm"));
        }
        private static double? Parse(JToken value) => double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;
        private sealed class ShipmentItem : INotifyPropertyChanged
        {
            public string Number { get; set; }
            public JObject Result { get; set; }
            public DateTime QueriedAt { get; set; }
            private string status = Scanner.WPF.Helpers.UiText.Get("WpfSupplement22");
            public string Status { get => status; set { status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}

