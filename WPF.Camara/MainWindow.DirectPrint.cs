using System.Printing;
using System.Windows;
using System.Windows.Controls;

namespace WPF.Camara;

public partial class MainWindow
{
    private CancellationTokenSource? printCancellation;

    private void RefreshPrinters_Click(object sender, RoutedEventArgs e) => LoadPrinters();

    private void LoadPrinters()
    {
        string? previous = Printer.SelectedItem as string;
        try
        {
            using var server = new LocalPrintServer();
            using var queues = server.GetPrintQueues([EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
            var names = queues.Select(x => x.FullName).OrderBy(x => x).ToArray();
            Printer.ItemsSource = names;
            Printer.SelectedItem = names.FirstOrDefault(x => x == previous)
                ?? names.FirstOrDefault(x => x.Contains("Rongta", StringComparison.OrdinalIgnoreCase))
                ?? names.FirstOrDefault(x => x.Contains("ZPL", StringComparison.OrdinalIgnoreCase));

            if (names.Length == 0) Status.Text = "未找到已安装的打印机。";
        }
        catch (Exception ex) { Status.Text = "读取打印机失败：" + ex.Message; }
    }

    private void StopSending_Click(object sender, RoutedEventArgs e)
    {
        printCancellation?.Cancel();
        Status.Text = "正在停止后续发送；已提交的任务仍可能继续出纸。";
    }

    private async Task SendDirectAsync(int count, Func<int, FrameworkElement> create, string title)
    {
        if (printCancellation != null) return;
        if (Printer.SelectedItem is not string printer) { Status.Text = "请先选择支持ZPL的标签打印机。"; return; }
        int dpi = PrinterDpi.SelectedIndex == 1 ? 300 : 203;
        printCancellation = new CancellationTokenSource();
        int sent = 0;
        PrintCurrentButton.IsEnabled = PrintBatchButton.IsEnabled = Mode.IsEnabled = Copies.IsEnabled = PrinterSettings.IsEnabled = false;
        StopSending.IsEnabled = true;
        SendProgress.Maximum = Math.Max(1, count); SendProgress.Value = 0;
        try
        {
            var token = printCancellation.Token;
            await LabelSendSequence.RunAsync(count, async i =>
            {
                var data = ZplLabelPrinter.Encode(create(i), dpi);
                await Task.Run(() => ZplLabelPrinter.Send(printer, $"{title}-{i + 1}/{count}", data));
            }, completed =>
            {
                sent = completed; SendProgress.Value = sent;
                Status.Text = $"{printer}：已逐张发送 {sent}/{count}，每张一个独立任务。";
            }, token);
            Status.Text = token.IsCancellationRequested ? $"已停止：成功提交 {sent}/{count} 张；已提交任务仍可能继续出纸。" : $"已逐张提交 {sent} 张到 {printer}，请核对实际出纸。";
        }
        catch (Exception ex) { Status.Text = $"发送中断：已成功提交 {sent}/{count} 张。第 {sent + 1} 张未确认，请先核对打印队列与出纸再补打。{ex.Message}"; }
        finally
        {
            printCancellation.Dispose(); printCancellation = null;
            PrintCurrentButton.IsEnabled = PrintBatchButton.IsEnabled = Mode.IsEnabled = Copies.IsEnabled = PrinterSettings.IsEnabled = true;
            StopSending.IsEnabled = false;
        }
    }
}

