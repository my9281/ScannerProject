using Microsoft.Win32;
using System.Windows;

namespace Scanner.Helpers
{
    public sealed class DialogHelper
    {
        public string SelectUrgentWorkOrderFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = Resource("SelectUrgentWorkOrderTitle"),
                Filter = Resource("UrgentWorkOrderFilter"),
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

        private static string Resource(string key)
        {
            object value = Application.Current.TryFindResource(key);
            return value == null ? key : value.ToString();
        }
    }
}
