using Microsoft.Win32;
using System.Windows;

namespace Scanner.Helpers
{
    public sealed class DialogHelper
    {
        public string SelectCsvFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择紧急工单 CSV 文件",
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                Multiselect = false
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void Information(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Warning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void Error(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
