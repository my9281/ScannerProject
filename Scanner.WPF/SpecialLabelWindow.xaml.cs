using Scanner.WPF.Helpers;
using System;
using System.Windows;
using System.Windows.Media;

namespace Scanner.WPF
{
    public partial class SpecialLabelWindow : Window
    {
        private readonly SpecialLabelKind _kind;
        private readonly PrintingHelper _printing = new PrintingHelper();

        public SpecialLabelWindow(SpecialLabelKind kind)
        {
            InitializeComponent();
            _kind = kind;
            LabelTitle.Text = UiText.Get(kind == SpecialLabelKind.TradeIn
                ? "WpfSpecialLabelTradeIn" : "WpfSpecialLabelScrapped");
            PreviewImage.Source = PrintingHelper.PreviewSpecialLabel(kind);
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(CopiesInput.Text, out int copies) || copies < 1 || copies > 999)
            {
                StatusText.Foreground = Brushes.DarkRed;
                StatusText.Text = UiText.Get("WpfSpecialLabelInvalidCopies");
                CopiesInput.Focus();
                CopiesInput.SelectAll();
                return;
            }
            PrintButton.IsEnabled = false;
            try
            {
                _printing.PrintSpecialLabel(_kind, copies);
                StatusText.Foreground = Brushes.DarkBlue;
                StatusText.Text = string.Format(UiText.Get("WpfSpecialLabelSent"), copies);
            }
            catch (Exception ex)
            {
                StatusText.Foreground = Brushes.DarkRed;
                StatusText.Text = UiText.Get("PrintFailedPrefix") + ex.Message;
            }
            finally { PrintButton.IsEnabled = true; }
        }
    }
}
