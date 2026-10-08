using Scanner.Helpers.Services;
using System;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
namespace Scanner.WPF
{
    public sealed class SkuSnBatchPicker : Window
    {
        private readonly ListBox _list = new();
        private readonly TextBlock _status = new();
        private sealed class BatchOption
        {
            public SkuSnBatchInfo Batch { get; set; }
            public override string ToString() => string.Format(Scanner.WPF.Helpers.UiText.Get("WpfBatchRecordCount"), Batch.BatchNumber, Batch.ItemCount, Batch.BatchId);
        }
        public SkuSnBatchInfo SelectedBatch { get; private set; }
        public SkuSnBatchPicker(ShelvedPalletApiService api)
        {
            SetResourceReference(IconProperty, "Logo"); SetResourceReference(TitleProperty, "WpfComplete222"); SetResourceReference(FontFamilyProperty, "AppFontFamily"); Width = 780; Height = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var download = new Button { Content = Scanner.WPF.Helpers.UiText.Get("WpfComplete223"), Padding = new Thickness(20,8,20,8), IsEnabled = false };
            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(_status); Grid.SetRow(_list,1); grid.Children.Add(_list); Grid.SetRow(download,2); grid.Children.Add(download); Content = grid;
            Loaded += async (_, _) => {
                try { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete224"); _list.ItemsSource = (await api.GetSkuSnBatchesAsync(ConfigurationManager.AppSettings["UploadApiKey"])).ConvertAll(x => new BatchOption { Batch = x }); _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete225"); download.IsEnabled = true; }
                catch (Exception ex) { _status.Text = Scanner.WPF.Helpers.UiText.ErrorMessage(ex); }
            };
            download.Click += async (_, _) => {
                if (!(_list.SelectedItem is BatchOption option)) { _status.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete226"); return; }
                var info = option.Batch;
                download.IsEnabled = false; _list.IsEnabled = false;
                try {
                    var batch = await api.GetSkuSnBatchAsync(info.BatchId,ConfigurationManager.AppSettings["UploadApiKey"]);
                    if (batch.BatchId != info.BatchId || batch.Items == null || batch.Items.Count != info.ItemCount) throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete227"));
                    SelectedBatch = batch; DialogResult = true;
                }
                catch (Exception ex) { _status.Text = Scanner.WPF.Helpers.UiText.ErrorMessage(ex); }
                finally { download.IsEnabled = true; _list.IsEnabled = true; }
            };
        }
    }
}

