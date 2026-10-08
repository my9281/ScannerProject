using Scanner.Helpers.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Scanner.WPF
{
    public partial class GoodAreaWindow : Window
    {
        private readonly ShelvedPalletApiService _api;
        private IReadOnlyList<PdaPalletRow> _rows = Array.Empty<PdaPalletRow>();
        private DateTime? _loadedAt;
        private bool _loading;
        public GoodAreaWindow(ShelvedPalletApiService api) { InitializeComponent(); _api = api; }
        private async void Window_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true; RefreshButton.IsEnabled = false;
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete138");
            try
            {
                var rows = await _api.GetPdaPalletsAsync(ConfigurationManager.AppSettings["UploadApiKey"]);
                _rows = rows; _loadedAt = DateTime.Now; ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete139") + ex.Message + (_loadedAt.HasValue ? Scanner.WPF.Helpers.UiText.Get("WpfComplete140") : Scanner.WPF.Helpers.UiText.Get("WpfComplete141"));
            }
            finally { _loading = false; RefreshButton.IsEnabled = true; }
        }
        private void Query_Click(object sender, RoutedEventArgs e) => ApplyFilter();
        private void PalletInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { ApplyFilter(); e.Handled = true; } }
        private void All_Click(object sender, RoutedEventArgs e) { PalletInput.Text = ""; ApplyFilter(); }
        private void ApplyFilter()
        {
            var text = PalletInput.Text.Trim();
            int? pallet = null;
            if (text.Length > 0)
            {
                if (!int.TryParse(text, out var number) || number < 1 || number > 100)
                { StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete142"); return; }
                pallet = number;
            }
            var visible = _rows.Where(x => !pallet.HasValue || x.PalletNumber == pallet.Value).OrderBy(x => x.PalletNumber).ThenBy(x => x.ScanTime).ToArray();
            ResultsGrid.ItemsSource = visible;
            StatusText.Text = string.Format(Scanner.WPF.Helpers.UiText.Get("WpfComplete143"), _rows.Count, visible.Length) + (pallet.HasValue ? string.Format(Scanner.WPF.Helpers.UiText.Get("WpfComplete144"), pallet.Value) : Scanner.WPF.Helpers.UiText.Get("WpfComplete145")) + (_loadedAt.HasValue ? string.Format(Scanner.WPF.Helpers.UiText.Get("WpfComplete146"), _loadedAt) : Scanner.WPF.Helpers.UiText.Get("WpfComplete147"));
        }
    }
}
