using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WPF.Camara;

public partial class MainWindow
{
    private ReceivingForecast forecast = ReceivingForecast.LoadDefault();
    private bool receivingReady;
    private bool IsReceiving => Mode.SelectedIndex == 2;
    private ReceivingRow? SelectedReceiving => ReceivingRows.SelectedItem as ReceivingRow;

    private void InitializeReceiving()
    {
        Pallet.ItemsSource = forecast.PalletChoices;
        Pallet.SelectedIndex = 0;
        LabelPolicy.SelectedIndex = 0;
        receivingReady = true;
        LoadPallet();
    }

    private void Pallet_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (receivingReady) LoadPallet();
    }

    private void LoadPallet()
    {
        if (Pallet.SelectedValue is not int pallet) return;
        ReceivingRows.ItemsSource = forecast.Rows.Where(x => x.Pallet == pallet).ToArray();
        ReceivingRows.SelectedIndex = 0;
        var rows = forecast.Rows.Where(x => x.Pallet == pallet).ToArray();
        PalletSummary.Text = $"{ReceivingForecast.PalletName(pallet)}：共 {rows.Sum(x => x.Quantity)} 件；可打印 {rows.Where(x => x.CanPrint).Sum(x => x.Quantity)} 件；跳过 {rows.Where(x => !x.CanPrint).Sum(x => x.Quantity)} 件。";
        UpdateReceivingPreview();
    }

    private void Receiving_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (receivingReady) UpdateReceivingPreview();
    }

    private void UpdateReceivingPreview()
    {
        if (!ready || !IsReceiving || SelectedReceiving is not ReceivingRow row) return;
        bool perPiece = LabelPolicy.SelectedIndex == 0;
        Copies.Text = (perPiece ? row.Quantity : 1).ToString();
        Configuration.Text = "4 × 6 英寸（101.6 × 152.4 mm）\n一维条码：表内校对码；二维码：完整 SKU。\n原始码不同的行会保留提示，打印前请核对实物。";
        Preview.Content = row.CanPrint ? ReceivingLabel.Create(row, 1, perPiece ? row.Quantity : 1, perPiece) : null;
        var jobs = forecast.BuildJobs(row.Pallet, perPiece);
        Status.Text = $"{row.PalletName}预计打印 {jobs.Sum(x => x.Copies)} 张；当前行：{row.State}";
    }

    private void ImportForecast_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "制表符预报表 (*.txt;*.tsv)|*.txt;*.tsv", Title = "选择收货预报表" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var imported = ReceivingForecast.Parse(File.ReadAllText(dialog.FileName));
            forecast = imported;
            receivingReady = false;
            Pallet.ItemsSource = forecast.PalletChoices;
            Pallet.SelectedIndex = 0;
            receivingReady = true;
            LoadPallet();
        }
        catch (Exception ex) { Status.Text = "导入失败，原有预报保持不变：" + ex.Message; }
    }

    private async void PrintReceiving(bool wholePallet)
    {
        if (Pallet.SelectedValue is not int pallet) return;
        bool perPiece = LabelPolicy.SelectedIndex == 0;
        IReadOnlyList<ReceivingLabelJob> jobs;
        if (wholePallet) jobs = forecast.BuildJobs(pallet, perPiece);
        else
        {
            if (SelectedReceiving is not ReceivingRow row || !row.CanPrint) { Status.Text = "当前行SKU或条码未确认，不能打印。"; return; }
            if (!int.TryParse(Copies.Text, out int copies) || copies < 1 || copies > 999) { Status.Text = "打印份数须为1–999。"; return; }
            jobs = [new(row, copies, perPiece)];
        }
        int count = jobs.Sum(x => x.Copies);
        if (count == 0) { Status.Text = "所选卡板没有已知SKU可打印。"; return; }
        var pages = jobs.SelectMany(job => Enumerable.Range(1, job.Copies).Select(index => (job, index))).ToArray();
        await SendDirectAsync(pages.Length, i => ReceivingLabel.Create(pages[i].job.Row, pages[i].index, pages[i].job.Copies, pages[i].job.PerPiece), ReceivingForecast.PalletName(pallet));
    }
}
