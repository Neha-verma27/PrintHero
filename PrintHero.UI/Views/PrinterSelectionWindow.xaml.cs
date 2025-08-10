using System.Windows;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using System.Collections.Generic;

namespace PrintHero.UI.Views
{
    public partial class PrinterSelectionWindow : Window
    {
        private readonly ILogger? _logger;
        
        public string SelectedPrinter { get; private set; } = string.Empty;

        public PrinterSelectionWindow(List<string> availablePrinters, ILogger? logger)
        {
            InitializeComponent();
            _logger = logger;
            
            // Populate the printer list
            foreach (var printer in availablePrinters)
            {
                PrinterListBox.Items.Add(printer);
            }
            
            // Select first printer by default if available
            if (PrinterListBox.Items.Count > 0)
            {
                PrinterListBox.SelectedIndex = 0;
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PrinterListBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a printer from the list.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedPrinter = PrinterListBox.SelectedItem.ToString() ?? string.Empty;
                _logger?.LogInformation($"Printer selected: {SelectedPrinter}");
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to select printer");
                MessageBox.Show($"Failed to select printer: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("Printer selection cancelled");
            DialogResult = false;
            Close();
        }

        private void PrinterListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Double-click to select
            Select_Click(sender, e);
        }
    }
}