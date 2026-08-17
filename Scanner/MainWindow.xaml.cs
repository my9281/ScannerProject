using Scanner.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace Scanner
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            _viewModel.FocusRequested += ViewModel_FocusRequested;
            Activated += MainWindow_Activated;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            FocusScannerInput();
        }

        private void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            InputMethod.SetPreferredImeState(SnTextBox, InputMethodState.Off);
        }

        private void ViewModel_FocusRequested(object sender, EventArgs e)
        {
            FocusScannerInput();
        }

        private void FocusScannerInput()
        {
            SnTextBox.Focus();
            Keyboard.Focus(SnTextBox);
            SnTextBox.SelectAll();
        }

        private void OpenChecklistButton_Click(object sender, RoutedEventArgs e)
        {
            ChecklistWindow window = new ChecklistWindow { Owner = this };
            window.ShowDialog();
            FocusScannerInput();
        }
    }
}
