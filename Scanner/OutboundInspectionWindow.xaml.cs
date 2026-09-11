using Microsoft.Win32;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using Scanner.Controllers;
using Scanner.PlatformControllers;
namespace Scanner
{
    public partial class OutboundInspectionWindow : Window, IOutboundInspectionWindowView
    {
        private readonly OutboundInspectionWindowController _controller;
        public OutboundInspectionWindow(IControllerFactory<IOutboundInspectionWindowView, OutboundInspectionWindowController> factory)
        {
            InitializeComponent();
            _controller = factory.Create(this);
            Closed += (s, e) => _controller.Dispose();
        }
        Window IOutboundInspectionWindowView.OwnerWindow => this;
        System.Windows.Controls.TextBlock IOutboundInspectionWindowView.TextFileTextBlock => TextFileTextBlock;
        System.Windows.Controls.TextBlock IOutboundInspectionWindowView.BaseFileTextBlock => BaseFileTextBlock;
        System.Windows.Controls.TextBox IOutboundInspectionWindowView.PalletNumberTextBox => PalletNumberTextBox;
        System.Windows.Controls.Button IOutboundInspectionWindowView.PrintButton => PrintButton;
        System.Windows.Controls.Button IOutboundInspectionWindowView.ExportButton => ExportButton;
        System.Windows.Controls.TextBlock IOutboundInspectionWindowView.SummaryTextBlock => SummaryTextBlock;
        System.Windows.Controls.DataGrid IOutboundInspectionWindowView.ResultDataGrid => ResultDataGrid;
        private void SelectTextButton_Click(object sender, RoutedEventArgs e) => _controller.SelectTextButton_Click(sender, e);
        private void PrintButton_Click(object sender, RoutedEventArgs e) => _controller.PrintButton_Click(sender, e);
        private void ExportButton_Click(object sender, RoutedEventArgs e) => _controller.ExportButton_Click(sender, e);
    }
}
