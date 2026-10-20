using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using Scanner.Helpers;
using Scanner.Helpers.Services;
using Microsoft.Web.WebView2.Core;

namespace Scanner.WPF
{
    public partial class BluettiSessionWindow : Window
    {
        private readonly BluettiSessionService _service = new BluettiSessionService();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private BluettiSession _session;
        private readonly ChecklistDataCache _baseData;
        private readonly Func<string, UrgentWorkOrderImportResult> _importForecast;
        private readonly BluettiApiExporter _exporter;
        private readonly BluettiConfigStore _config = new BluettiConfigStore();
        private readonly bool _settingsMode;
        private readonly bool _autoStart;
        private string _validatedUsername;

        public BluettiSessionWindow() : this(null, null) { }

        public BluettiSessionWindow(ChecklistDataCache baseData, Func<string, UrgentWorkOrderImportResult> importForecast, bool settingsMode = false, bool autoStart = false)
        {
            InitializeComponent();
            _baseData = baseData;
            _importForecast = importForecast;
            _settingsMode = settingsMode;
            _autoStart = autoStart;
            _exporter = new BluettiApiExporter();
            SettingsPanel.Visibility = settingsMode ? Visibility.Visible : Visibility.Collapsed;
            SyncPanel.Visibility = settingsMode ? Visibility.Collapsed : Visibility.Visible;
            if (settingsMode)
            {
                SetResourceReference(TitleProperty, "WpfSystemSettings");
                DescriptionText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "WpfSystemSettingsDescription");
                BrowserBorder.Visibility = Visibility.Collapsed;
                MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
                MinWidth = 500; MinHeight = 700; Width = 580; Height = 800;
                ConfigPathText.Text = _config.FilePath;
            }
            Closed += (_, _) => { _cancellation.Cancel(); ExportBrowser.Dispose(); };
        }

        private async Task RunAsync(Func<Task> action)
        {
            FormPanel.IsEnabled = false;
            StatusText.Text = UiText.Get("WpfBluettiBusy");
            try { await action(); }
            catch (BluettiAuthenticationException)
            {
                _session = null;
                SiteBox.ItemsSource = null;
                SaveButton.IsEnabled = false;
                SetSyncEnabled(false);
                try { _service.Clear(); }
                catch { /* Authentication remains invalid even if the file cannot be removed. */ }
                StatusText.Text = UiText.Get("WpfBluettiExpired");
            }
            catch (WebView2RuntimeNotFoundException) { Log(UiText.Get("WpfAutoSyncRuntimeMissing")); }
            catch (TimeoutException) { Log(UiText.Get("WpfAutoSyncTimeout")); }
            catch (OperationCanceledException) { Log(UiText.Get("WpfAutoSyncTimeout")); }
            catch (Exception) { Log(UiText.Get("WpfBluettiFailed")); }
            finally { FormPanel.IsEnabled = true; }
        }

        private void SetSyncEnabled(bool enabled)
        {
            SyncAllButton.IsEnabled = enabled && _baseData != null && _importForecast != null;
            SyncForecastButton.IsEnabled = enabled && _importForecast != null;
            SyncBaseButton.IsEnabled = enabled && _baseData != null;
        }

        private async Task LoadSitesAsync()
        {
            var sites = await _service.GetSitesAsync(_session, _cancellation.Token);
            SiteBox.ItemsSource = sites;
            SiteBox.SelectedItem = sites.FirstOrDefault(s => s.SiteId == _session.SiteId) ??
                (_settingsMode || sites.Count == 1 ? sites.FirstOrDefault() : null);
            SaveButton.IsEnabled = sites.Count > 0;
            SetSyncEnabled(SiteBox.SelectedItem != null);
            StatusText.Text = UiText.Get(sites.Count > 0 ? "WpfBluettiChoose" : "WpfBluettiNoSites");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                var config = _config.Load();
                if (_settingsMode && config != null)
                {
                    AccountBox.Text = config.Username;
                    PasswordInput.Password = config.Password;
                    RememberCredentials.IsChecked = true;
                }
                _session = _service.Load();
                if (_session != null)
                {
                    try { await _service.ValidateAsync(_session, _cancellation.Token); }
                    catch (BluettiAuthenticationException) { _service.Clear(); _session = null; }
                }
                if (_session == null && config != null && !_settingsMode)
                {
                    _session = await _service.LoginAsync(config.Username, config.Password, _cancellation.Token);
                    _session.SiteId = config.SiteId;
                }
                if (_session == null) { StatusText.Text = UiText.Get("WpfConfigRequired"); return; }
                _validatedUsername = config?.Username;
                await LoadSitesAsync();
                var selected = SiteBox.SelectedItem as BluettiSite;
                if (selected != null && (!_settingsMode || selected.SiteId == _session.SiteId))
                {
                    await _service.SelectSiteAsync(_session, selected, _cancellation.Token);
                    _service.Save(_session);
                    StatusText.Text = string.Format(UiText.Get("WpfBluettiRestored"), _session.DisplayName, _session.SiteName);
                }
                else if (!_settingsMode) StatusText.Text = UiText.Get("WpfConfigRequired");
            });
            if (_autoStart && !_settingsMode && SyncAllButton.IsEnabled)
                await SyncAsync(true, true);
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AccountBox.Text) || PasswordInput.SecurePassword.Length == 0)
            { StatusText.Text = UiText.Get("WpfBluettiRequired"); return; }
            await RunAsync(async () =>
            {
                _session = null;
                SaveButton.IsEnabled = false;
                SetSyncEnabled(false);
                SiteBox.ItemsSource = null;
                _service.Clear();
                try { _session = await _service.LoginAsync(AccountBox.Text, PasswordInput.Password, _cancellation.Token); }
                finally { if (!_settingsMode) PasswordInput.Clear(); }
                _validatedUsername = AccountBox.Text.Trim();
                await LoadSitesAsync();
            });
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || !(SiteBox.SelectedItem is BluettiSite site)) return;
            if (_settingsMode && !string.Equals(AccountBox.Text.Trim(), _validatedUsername, StringComparison.OrdinalIgnoreCase))
            { StatusText.Text = UiText.Get("WpfConfigLoginRequired"); return; }
            await RunAsync(async () =>
            {
                await _service.SelectSiteAsync(_session, site, _cancellation.Token);
                _service.Save(_session);
                if (_settingsMode) SaveConfiguration();
                StatusText.Text = string.Format(UiText.Get("WpfBluettiSaved"), _session.DisplayName, _session.SiteName);
            });
        }

        private void SaveConfiguration()
        {
            if (RememberCredentials.IsChecked != true) { _config.Clear(); return; }
            _config.Save(new BluettiLoginConfig
            {
                Username = AccountBox.Text,
                Password = PasswordInput.Password,
                SiteId = string.Equals(AccountBox.Text.Trim(), _validatedUsername, StringComparison.OrdinalIgnoreCase)
                    ? (SiteBox.SelectedItem as BluettiSite)?.SiteId : null
            });
        }

        private async void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (RememberCredentials.IsChecked == true &&
                (string.IsNullOrWhiteSpace(AccountBox.Text) || PasswordInput.SecurePassword.Length == 0))
            { StatusText.Text = UiText.Get("WpfBluettiRequired"); return; }
            await RunAsync(() =>
            {
                SaveConfiguration();
                // Reauthenticate with the newly configured credentials on the next sync.
                _service.Clear();
                StatusText.Text = UiText.Get(RememberCredentials.IsChecked == true ? "WpfConfigSaved" : "WpfConfigRemoved");
                return Task.CompletedTask;
            });
        }

        private async void Clear_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(() =>
            {
                _service.Clear();
                _session = null;
                SiteBox.ItemsSource = null;
                SaveButton.IsEnabled = false;
                SetSyncEnabled(false);
                PasswordInput.Clear();
                StatusText.Text = UiText.Get("WpfBluettiCleared");
                return Task.CompletedTask;
            });
        }

        private void Log(string text)
        {
            StatusText.Text = text;
            ProgressLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
            ProgressLog.ScrollToEnd();
        }

        private async void SyncAll_Click(object sender, RoutedEventArgs e) => await SyncAsync(true, true);
        private async void SyncForecast_Click(object sender, RoutedEventArgs e) => await SyncAsync(true, false);
        private async void SyncBase_Click(object sender, RoutedEventArgs e) => await SyncAsync(false, true);

        private async Task SyncAsync(bool forecast, bool baseData)
        {
            if (_session == null || !(SiteBox.SelectedItem is BluettiSite site)) return;
            await RunAsync(async () =>
            {
                await _service.ValidateAsync(_session, _cancellation.Token);
                await _service.SelectSiteAsync(_session, site, _cancellation.Token);
                _service.Save(_session);
                // Prevent interactive changes to filters, site or export buttons during synchronization.
                ExportBrowser.IsEnabled = false;
                try
                {
                    if (forecast)
                    {
                        Log(UiText.Get("WpfAutoSyncForecastDownloading"));
                        var path = await _exporter.ExportAsync(_session, true, _cancellation.Token);
                        var result = _importForecast(path);
                        Log(string.Format(UiText.Get("WpfAutoSyncForecastDone"), result.ImportedRowCount));
                    }
                    if (baseData)
                    {
                        Log(UiText.Get("WpfAutoSyncBaseDownloading"));
                        var path = await _exporter.ExportAsync(_session, false, _cancellation.Token);
                        // Parse fully before replacing the shared cache, just as manual import does.
                        await Task.Run(() => _baseData.ImportBase(path), _cancellation.Token);
                        Log(string.Format(UiText.Get("WpfAutoSyncBaseDone"), _baseData.Records.Count));
                    }
                    Log(UiText.Get("WpfAutoSyncDone"));
                }
                finally { ExportBrowser.IsEnabled = true; }
            });
        }
    }
}
