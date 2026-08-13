using Scanner.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Scanner
{
    public partial class MeterModelSelectionWindow : Window
    {
        private readonly MeterModelService _meterModels;

        public MeterModelSelectionWindow(MeterModelService meterModels)
        {
            _meterModels = meterModels ?? throw new ArgumentNullException(nameof(meterModels));
            Models = new ObservableCollection<string>(_meterModels.GetModels());
            InitializeComponent();
            DataContext = this;
        }

        public ObservableCollection<string> Models { get; }
        public string SelectedModel { get; set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Models.Count > 0)
            {
                ModelListBox.SelectedIndex = 0;
                ModelListBox.Focus();
            }
            else
            {
                NewModelTextBox.Focus();
            }
        }

        private void ModelListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                UseSelectedModel();
            }
        }

        private void ModelListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            UseSelectedModel();
        }

        private void NewModelTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                AddAndUseModel();
            }
        }

        private void UseSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            UseSelectedModel();
        }

        private void AddAndPrintButton_Click(object sender, RoutedEventArgs e)
        {
            AddAndUseModel();
        }

        private void UseSelectedModel()
        {
            string selected = ModelListBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selected))
            {
                ShowError(Resource("SelectModelRequired"));
                return;
            }
            SelectedModel = selected;
            DialogResult = true;
        }

        private void AddAndUseModel()
        {
            try
            {
                string model = _meterModels.AddCustomModel(NewModelTextBox.Text);
                if (!Models.Contains(model))
                {
                    Models.Add(model);
                }
                SelectedModel = model;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                NewModelTextBox.SelectAll();
                NewModelTextBox.Focus();
            }
        }

        private void ShowError(string message)
        {
            StatusTextBlock.Text = message;
        }

        private static string Resource(string key)
        {
            object value = Application.Current.TryFindResource(key);
            return value == null ? key : value.ToString();
        }
    }
}
