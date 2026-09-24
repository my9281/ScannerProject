using Scanner.WPF.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Scanner.WPF.Views
{
    public partial class SkuSerialLabelView : UserControl
    {
        private readonly PrintingHelper _printing = new PrintingHelper();

        public SkuSerialLabelView()
        {
            InitializeComponent();
            Loaded += (sender, args) => SkuInput.Focus();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            string sku = (SkuInput.Text ?? string.Empty).Trim();
            string serialNumber = (SerialNumberInput.Text ?? string.Empty).Trim();
            string remark = (RemarkInput.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sku)) { ShowError("请输入 SKU。"); SkuInput.Focus(); return; }
            if (string.IsNullOrWhiteSpace(serialNumber)) { ShowError("请输入 SN。"); SerialNumberInput.Focus(); return; }
            if (string.IsNullOrWhiteSpace(remark)) { ShowError("请输入备注。"); RemarkInput.Focus(); return; }

            try
            {
                _printing.PrintSkuSerialNumberLabel(sku, serialNumber, remark);
                StatusText.Foreground = Brushes.DarkBlue;
                StatusText.Text = "标签已发送到默认打印机。";
                SerialNumberInput.SelectAll();
                SerialNumberInput.Focus();
            }
            catch (Exception ex)
            {
                ShowError("打印失败：" + ex.Message);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            SkuInput.Clear();
            SerialNumberInput.Clear();
            RemarkInput.Clear();
            StatusText.Foreground = Brushes.DarkBlue;
            StatusText.Text = "等待输入";
            SkuInput.Focus();
        }

        private void ShowError(string message)
        {
            StatusText.Foreground = Brushes.DarkRed;
            StatusText.Text = message;
        }
    }
}
