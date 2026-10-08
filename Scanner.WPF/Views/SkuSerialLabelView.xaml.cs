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
            if (string.IsNullOrWhiteSpace(sku)) { ShowError(Scanner.WPF.Helpers.UiText.Get("WpfPrompt012")); SkuInput.Focus(); return; }
            if (string.IsNullOrWhiteSpace(serialNumber)) { ShowError(Scanner.WPF.Helpers.UiText.Get("WpfPrompt013")); SerialNumberInput.Focus(); return; }
            if (string.IsNullOrWhiteSpace(remark)) { ShowError(Scanner.WPF.Helpers.UiText.Get("WpfPrompt014")); RemarkInput.Focus(); return; }

            try
            {
                _printing.PrintSkuSerialNumberLabel(sku, serialNumber, remark);
                StatusText.Foreground = Brushes.DarkBlue;
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfPrompt015");
                SerialNumberInput.SelectAll();
                SerialNumberInput.Focus();
            }
            catch (Exception ex)
            {
                ShowError(Scanner.WPF.Helpers.UiText.Get("PrintFailedPrefix") + ex.Message);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            SkuInput.Clear();
            SerialNumberInput.Clear();
            RemarkInput.Clear();
            StatusText.Foreground = Brushes.DarkBlue;
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete108");
            SkuInput.Focus();
        }

        private void ShowError(string message)
        {
            StatusText.Foreground = Brushes.DarkRed;
            StatusText.Text = message;
        }
    }
}
