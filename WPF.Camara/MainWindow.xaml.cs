using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ZXing;
using ZXing.QrCode;

namespace WPF.Camara;

public partial class MainWindow : Window
{
    private void StartRepair_Click(object sender, RoutedEventArgs e)
    {
        try { new RepairStartWindow { Owner = this }.ShowDialog(); }
        catch (Exception ex) { MessageBox.Show(this, "无法打开开始维修录入，请检查配置、待上传文件或是否有另一窗口正在使用：" + ex.Message, "开始维修"); }
    }

    private bool ready;
    public MainWindow()
    {
        InitializeComponent();
        Sku.ItemsSource = CameraModel.All;
        Sku.DisplayMemberPath = nameof(CameraModel.Sku);
        Sku.SelectedIndex = 0;
        SetSpecifications();
        Catalog.ItemsSource = LabelCatalog.Create();
        Catalog.SelectedIndex = 0;
        ready = true;
        Copies.Text = ((LabelItem)Catalog.SelectedItem).Quantity.ToString();
        InitializeReceiving();
        Mode.SelectedIndex = 2;
        LoadPrinters();
        Closed += (_, _) => printCancellation?.Cancel();
        UpdatePreview();
    }

    private void RefreshPreview(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        if (ReferenceEquals(sender, Sku)) SetSpecifications();
        UpdatePreview();
    }

    private bool TrySize(out double width, out double height)
    {
        bool valid = double.TryParse(LabelWidth.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out width)
            & double.TryParse(LabelHeight.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out height);
        return valid && double.IsFinite(width) && double.IsFinite(height) && width >= 60 && width <= 200 && height >= 50 && height <= 200;
    }

    private void UpdatePreview()
    {
        if (IsReceiving) { UpdateReceivingPreview(); return; }
        if (!TrySize(out double width, out double height))
        {
            Preview.Content = null;
            Status.Text = "标签宽度须为 60–200 mm，高度须为 50–200 mm。";
            return;
        }
        Configuration.Text = (Mode.SelectedIndex == 0 ? $"核对码（不打印）：{((LabelItem)Catalog.SelectedItem).CheckCodes}\n" : "") + CameraModel.Describe(SelectedLabelSku);
        Preview.Content = CreateLabel(width * 96 / 25.4, height * 96 / 25.4, SelectedLabelSku);
        Status.Text = $"二维码内容：{SelectedLabelSku} · {width} × {height} mm";
    }

    private void SetSpecifications()
    {
        bool previous = ready;
        ready = false;
        Meters.ItemsSource = ((CameraModel)Sku.SelectedItem).VariantSkus;
        Meters.SelectedIndex = 0;
        ready = previous;
    }

    private string SelectedLabelSku => Mode.SelectedIndex == 0 ? ((LabelItem)Catalog.SelectedItem).Sku : (string)Meters.SelectedItem;
    public static FrameworkElement CreateLabel(double width, double height, string labelSku)
    {
        var qr = new QRCodeWriter().encode(labelSku, BarcodeFormat.QR_CODE, 0, 0);
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, qr.Width, qr.Height));
            for (int y = 0; y < qr.Height; y++)
                for (int x = 0; x < qr.Width; x++)
                    if (qr[x, y]) context.DrawRectangle(Brushes.Black, null, new Rect(x, y, 1, 1));
        }
        drawing.Freeze();
        var image = new Image { Source = new DrawingImage(drawing), Width = 170, Height = 170, Stretch = Stretch.Uniform };
        RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);
        var content = new StackPanel { Width = 354, Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(image);
        content.Children.Add(new TextBlock { Text = labelSku, FontFamily = new FontFamily("Arial"), FontSize = 21, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.Black });
        return new Border { Width = width, Height = height, Background = Brushes.White, Child = new Viewbox { Stretch = Stretch.Uniform, Child = content } };
    }
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (IsReceiving) { PrintReceiving(false); return; }
        if (!TrySize(out double width, out double height)) { UpdatePreview(); return; }
        if (!int.TryParse(Copies.Text, out int copies) || copies < 1 || copies > 999)
        { Status.Text = "打印份数须为 1–999 的整数。"; return; }
        PrintJobs([new(SelectedLabelSku, copies)]);
    }

    private void Catalog_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || Catalog.SelectedItem is not LabelItem item) return;
        Copies.Text = item.Quantity.ToString();
        UpdatePreview();
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || e.Source != Mode) return;
        LabelWidth.IsEnabled = LabelHeight.IsEnabled = !IsReceiving;
        PrintCurrentButton.Content = IsReceiving ? "打印当前行4×6标签" : "打印当前 SKU";
        PrintBatchButton.Content = IsReceiving ? "打印所选整板4×6标签" : "按清单数量批量打印";
        if (IsReceiving) { UpdateReceivingPreview(); return; }
        Copies.Text = Mode.SelectedIndex == 0 ? ((LabelItem)Catalog.SelectedItem).Quantity.ToString() : "1";
        UpdatePreview();
    }

    private void PrintAll_Click(object sender, RoutedEventArgs e)
    {
        if (IsReceiving) { PrintReceiving(true); return; }
        if (Mode.SelectedIndex != 0) { Status.Text = "请切换到本次打印清单后批量打印。"; return; }
        if (!Catalog.CommitEdit(DataGridEditingUnit.Cell, true) || !Catalog.CommitEdit(DataGridEditingUnit.Row, true))
        { Status.Text = "请先修正清单中的数量。"; return; }
        try { PrintJobs(LabelCatalog.BuildJobs((IEnumerable<LabelItem>)Catalog.ItemsSource)); }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    public static FixedDocument CreateDocument(IReadOnlyList<LabelJob> jobs, double w, double h)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(w, h);
        foreach (var job in jobs)
        for (int i = 0; i < job.Copies; i++)
        {
            var page = new FixedPage { Width = w, Height = h };
            page.Children.Add(CreateLabel(w, h, job.Sku));
            document.Pages.Add(new PageContent { Child = page });
        }
        return document;
    }

    private async void PrintJobs(IReadOnlyList<LabelJob> jobs)
    {
        if (!TrySize(out double width, out double height)) { UpdatePreview(); return; }
        var labels = jobs.SelectMany(job => Enumerable.Repeat(job.Sku, job.Copies)).ToArray();
        if (labels.Length == 0) { Status.Text = "没有需要打印的标签。"; return; }
        await SendDirectAsync(labels.Length, i => CreateLabel(width * 96 / 25.4, height * 96 / 25.4, labels[i]), "SKU标签");
    }
}

