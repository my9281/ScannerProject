using Scanner.Helpers.Services;
using Scanner.WPF.Helpers;
using Microsoft.Win32;
using Scanner.Models;
using Scanner.WPF.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

using Scanner.Controllers;
using Scanner.WPF.Controllers;
namespace Scanner.WPF
{
    public partial class ChecklistWindow : Window, IChecklistWindowView
    {
        private readonly ChecklistWindowController _controller;
        public ChecklistWindow(IControllerFactory<IChecklistWindowView, ChecklistWindowController> factory)
        {
            InitializeComponent();
            _controller = factory.Create(this);
            Closed += (s, e) => _controller.Dispose();
        }
        Window IChecklistWindowView.OwnerWindow => this;
        System.Windows.Controls.Button IChecklistWindowView.ExportButton => ExportButton;
        System.Windows.Controls.Button IChecklistWindowView.ExportSimplifiedButton => ExportSimplifiedButton;
        System.Windows.Controls.Button IChecklistWindowView.ExportCurrentMonthButton => ExportCurrentMonthButton;
        System.Windows.Controls.DataGrid IChecklistWindowView.StatusSummaryDataGrid => StatusSummaryDataGrid;
        System.Windows.Controls.DataGridTextColumn IChecklistWindowView.CurrentMonthColumn => CurrentMonthColumn;
        System.Windows.Controls.DataGridTextColumn IChecklistWindowView.PreviousMonthColumn => PreviousMonthColumn;
        System.Windows.Controls.DataGridTextColumn IChecklistWindowView.TwoMonthsAgoColumn => TwoMonthsAgoColumn;
        System.Windows.Controls.TextBlock IChecklistWindowView.BaseStatusTextBlock => BaseStatusTextBlock;
        System.Windows.Controls.TextBlock IChecklistWindowView.BaseFileTextBlock => BaseFileTextBlock;
        System.Windows.Controls.TextBlock IChecklistWindowView.SnStatusTextBlock => SnStatusTextBlock;
        System.Windows.Controls.TextBlock IChecklistWindowView.SnFileTextBlock => SnFileTextBlock;
        private void ImportSnButton_Click(object sender, RoutedEventArgs e) => _controller.ImportSnButton_Click(sender, e);
        private void ExportButton_Click(object sender, RoutedEventArgs e) => _controller.ExportButton_Click(sender, e);
        private void ExportSimplifiedButton_Click(object sender, RoutedEventArgs e) => _controller.ExportSimplifiedButton_Click(sender, e);
        private void ExportCurrentMonthButton_Click(object sender, RoutedEventArgs e) => _controller.ExportCurrentMonthButton_Click(sender, e);
    }
}
