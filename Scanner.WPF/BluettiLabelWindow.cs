using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Scanner.WPF.Services;

namespace Scanner.WPF
{
    public sealed class BluettiLabelWindow : Window
    {
        private readonly TextBox _input = new TextBox { Height = 34, VerticalContentAlignment = VerticalAlignment.Center };
        private readonly DatePicker _date = new DatePicker { SelectedDate = DateTime.Today, Width = 145 };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        private readonly TextBlock _pathText = new TextBlock { Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow0"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _query = new Button { Content = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow1"), Padding = new Thickness(14, 6, 14, 6) };
        private readonly Button _choose = new Button { Content = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow2"), Padding = new Thickness(14, 6, 14, 6) };
        private readonly Button _cancel = new Button { Content = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow3"), IsEnabled = false, Padding = new Thickness(14, 6, 14, 6) };
        private readonly ObservableCollection<BluettiLabelRow> _rows = new ObservableCollection<BluettiLabelRow>();
        private readonly DataGrid _grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true };
        private readonly BluettiLabelCaptureService _capture = new BluettiLabelCaptureService();
        private CancellationTokenSource _cancellation;
        private bool _closeRequested;
        private string _path;

        public BluettiLabelWindow()
        {
            Title = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow4"); Width = 1200; Height = 700; MinWidth = 850; MinHeight = 540;
            SetResourceReference(IconProperty, "Logo");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var layout = new Grid { Margin = new Thickness(20) };
            foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star) }) layout.RowDefinitions.Add(new RowDefinition { Height = height });
            void Add(UIElement item, int row) { Grid.SetRow(item, row); layout.Children.Add(item); }
            Add(new TextBlock { Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow5"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }, 0);
            var fileRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(_choose, Dock.Right); fileRow.Children.Add(_choose); fileRow.Children.Add(_pathText); Add(fileRow, 1);
            Add(_input, 2);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            actions.Children.Add(new TextBlock { Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow6"), VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(_date);
            var shipping = new Button { Content = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow7"), Margin = new Thickness(10, 0, 0, 0) };
            shipping.Click += (_, _) => { if (_grid.SelectedItem is BluettiLabelRow row) new OmsShippingWindow(row) { Owner = this }.ShowDialog(); else _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow8"); };
            actions.Children.Add(shipping);
            foreach (var button in new[] { _query, _cancel }) { button.Margin = new Thickness(10, 0, 0, 0); actions.Children.Add(button); }
            Add(actions, 3); Add(_status, 4); Add(_grid, 5);
            var properties = new[] { "Date", "Sn", "Sku", "Model", "Dimensions", "Weight", "Price", "Fedex", "Carton", "Rma", "WorkOrder", "Name", "Email", "Phone", "Address", "Remark" };
            for (int i = 0; i < properties.Length; i++) _grid.Columns.Add(new DataGridTextColumn { Header = Scanner.WPF.Helpers.UiText.Get("WpfLabelColumn" + i), Binding = new Binding(properties[i]), Width = new DataGridLength(i == 14 ? 350 : 160) });
            _grid.ItemsSource = _rows;
            _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow9");
            Content = layout;
            _query.Click += Query;
            _input.KeyDown += (sender, e) => { if (e.Key == Key.Enter && _cancellation == null) { e.Handled = true; Query(sender, e); } };
            _choose.Click += (_, _) => ChooseFile();
            _cancel.Click += (_, _) => _cancellation?.Cancel();
            Loaded += (_, _) => _input.Focus();
            Closing += (_, e) => { if (_cancellation != null) { e.Cancel = true; _closeRequested = true; _cancellation.Cancel(); } };
        }

        private bool ChooseFile()
        {
            var dialog = new SaveFileDialog { Filter = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow10") + "|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, OverwritePrompt = false, Title = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow11"), FileName = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow12") + DateTime.Today.ToString("yyyyMMdd") + ".xlsx" };
            if (dialog.ShowDialog(this) != true) return false;
            _path = dialog.FileName; _pathText.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow13") + _path;
            // Selection alone does not create a workbook. A miss leaves the path untouched.
            return true;
        }

        private async void Query(object sender, RoutedEventArgs e)
        {
            if (_cancellation != null) return;
            string sn;
            try { sn = BluettiLabelApi.ValidateSn(_input.Text); }
            catch (ArgumentException ex) { _status.Text = ex.Message; return; }
            if (!_date.SelectedDate.HasValue) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow14"); return; }
            if (_path == null && !ChooseFile()) return;
            _query.IsEnabled = _choose.IsEnabled = _input.IsEnabled = _date.IsEnabled = false; _cancel.IsEnabled = true;
            _cancellation = new CancellationTokenSource();
            try
            {
                _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow15") + sn;
                var session = await new BluettiSyncService().PrepareAsync(_cancellation.Token);
                var row = await _capture.CaptureAsync(session, sn, _date.SelectedDate.Value, _path, _cancellation.Token);
                if (row == null) _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow16") + sn + Scanner.WPF.Helpers.UiText.Get("WpfNewWindow17");
                else
                {
                    _rows.Add(row); _grid.ScrollIntoView(row); _input.Clear();
                    _status.Text = string.Format(Scanner.WPF.Helpers.UiText.Get("WpfNewWindow18"), row.Sn, _rows.Count);
                }
            }
            catch (OperationCanceledException) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow19"); }
            catch (BluettiConfigurationException) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow20"); }
            catch (BluettiAuthenticationException) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow21"); }
            catch (IOException ex) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow22") + ex.Message; }
            catch (Exception ex) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfNewWindow23") + ex.Message; }
            finally
            {
                _cancellation.Dispose(); _cancellation = null;
                _query.IsEnabled = _choose.IsEnabled = _input.IsEnabled = _date.IsEnabled = true; _cancel.IsEnabled = false;
                _input.Focus(); _input.SelectAll();
                if (_closeRequested) Close();
            }
        }
    }
}
