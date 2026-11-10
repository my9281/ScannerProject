using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ZXing;
using ZXing.Common;
using ZXing.OneD;
using ZXing.QrCode;

namespace WPF.Camara;

public static class ReceivingLabel
{
    public const double Width = 384; // 4 inches at 96 DIP/inch.
    public const double Height = 576; // 6 inches.

    public static FrameworkElement Create(ReceivingRow row, int index = 1, int copies = 1, bool perPiece = true)
    {
        if (!row.CanPrint) throw new ArgumentException("SKU或条码未确认，不能生成标签。");
        var body = new StackPanel { Margin = new Thickness(18), Width = Width - 36 };
        body.Children.Add(Text($"收货贴标    {row.PalletName}", 20, true));
        body.Children.Add(Text($"预报第 {row.Line} 行    该行数量：{row.Quantity} 件", 12));
        body.Children.Add(Text(perPiece ? $"逐件标签 {index}/{copies}" : "作业标签：本行一张，按行内数量贴标", 12));
        body.Children.Add(new Border { Height = 1, Background = Brushes.Black, Margin = new Thickness(0, 8, 0, 8) });
        body.Children.Add(Text("SKU / 产品型号", 11));
        body.Children.Add(new Viewbox { Width = Width - 36, Height = 30, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left, Child = Text(row.Sku, 23, true) });
        body.Children.Add(Text("条码（预报表校对码 / Code 128）", 10));
        var barcode = new Code128Writer().encode(row.Barcode, BarcodeFormat.CODE_128, 0, 60, new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 12 });
        body.Children.Add(CodeImage(barcode, 338, 58, "ProductBarcode"));
        body.Children.Add(Text(row.Barcode, 18, true, true));

        var detail = new Grid { Margin = new Thickness(0, 8, 0, 0), Height = 130 };
        detail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        detail.ColumnDefinitions.Add(new ColumnDefinition());
        // Supplied configuration screenshots cover S8951, not S8981; do not infer features from similar names.
        var description = row.Sku.StartsWith("S8981", StringComparison.Ordinal) ? "此 SKU 暂无配置说明。" : CameraModel.Describe(row.Sku);
        if (description == "此 SKU 暂无配置说明。") description = "产品配置：待补充资料\n按本页 SKU 核对产品与说明书";
        detail.Children.Add(new TextBlock { Text = description, FontSize = 11, LineHeight = 18, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Top });
        var qrPanel = new StackPanel();
        var qr = new QRCodeWriter().encode(row.Sku, BarcodeFormat.QR_CODE, 0, 0, new Dictionary<EncodeHintType, object> { [EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.Q, [EncodeHintType.QR_MASK_PATTERN] = 0 });
        qrPanel.Children.Add(CodeImage(qr, qr.Width * 3, qr.Height * 3, "SkuQrCode"));
        qrPanel.Children.Add(Text("SKU 二维码", 10, false, true));
        Grid.SetColumn(qrPanel, 1); detail.Children.Add(qrPanel); body.Children.Add(detail);
        body.Children.Add(new Border { Height = 1, Background = Brushes.Black, Margin = new Thickness(0, 6, 0, 6) });
        body.Children.Add(Text($"原始码：{row.OriginalCode}", 11));
        body.Children.Add(new TextBlock { Text = row.State, FontSize = 11, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 5) });
        body.Children.Add(Text("实物核对：________    贴标人：________", 12));
        body.Children.Add(Text("标签纸：4×6英寸 / 101.6×152.4 mm", 9));
        return new Border { Width = Width, Height = Height, Background = Brushes.White, Child = body };
    }

    private static TextBlock Text(string text, double size, bool bold = false, bool center = false) => new()
    {
        Text = text, FontSize = size, FontFamily = new FontFamily("Microsoft YaHei"), Foreground = Brushes.Black,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(0, 2, 0, 2),
        HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left
    };

    private static Image CodeImage(BitMatrix matrix, double width, double height, string name)
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, matrix.Width, matrix.Height));
            for (int y = 0; y < matrix.Height; y++)
            for (int x = 0; x < matrix.Width; x++)
                if (matrix[x, y]) dc.DrawRectangle(Brushes.Black, null, new Rect(x, y, 1, 1));
        }
        drawing.Freeze();
        var image = new Image { Name = name, Source = new DrawingImage(drawing), Width = width, Height = height, Stretch = Stretch.Fill };
        // QR matrices must retain their square modules.
        if (name == "SkuQrCode") image.Stretch = Stretch.Uniform;
        RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);
        return image;
    }

    public static FixedDocument Document(IReadOnlyList<ReceivingLabelJob> jobs, Rect? printable = null)
    {
        var doc = new FixedDocument();
        doc.DocumentPaginator.PageSize = new Size(Width, Height);
        var area = printable ?? new Rect(0, 0, Width, Height);
        foreach (var job in jobs)
        for (int i = 1; i <= job.Copies; i++)
        {
            var page = new FixedPage { Width = Width, Height = Height };
            var label = Create(job.Row, i, job.Copies, job.PerPiece);
            double scale = Math.Min(1, Math.Min(area.Width / Width, area.Height / Height));
            label.RenderTransform = new ScaleTransform(scale, scale);
            FixedPage.SetLeft(label, area.X + (area.Width - Width * scale) / 2);
            FixedPage.SetTop(label, area.Y + (area.Height - Height * scale) / 2);
            page.Children.Add(label); doc.Pages.Add(new PageContent { Child = page });
        }
        return doc;
    }
}





