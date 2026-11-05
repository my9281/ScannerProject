using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Scanner.WPF.Views
{
    public partial class ScanView : UserControl
    {
        public ScanView()
        {
            InitializeComponent();
            DashboardPluginHost.Content = Services.DashboardPluginLoader.Load(
                AppContext.BaseDirectory, Services.DesktopUpdateService.LocalVersion);
            if (DashboardPluginHost.Content == null)
                HeaderRow.Height = new GridLength(58);
        }

        public event EventHandler<KeyboardFocusChangedEventArgs> ScanInputFocused;
        private void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ScanInputFocused?.Invoke(sender, e);
    }
}
