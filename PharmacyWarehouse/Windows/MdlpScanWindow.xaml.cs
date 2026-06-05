using PharmacyWarehouse.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PharmacyWarehouse.Windows;

public partial class MdlpScanWindow : Window
{
    private readonly InventoryLine _inventoryLine;
    private ObservableCollection<string> _scannedCodes;
    private Stack<string> _undoStack = new();

    public MdlpScanWindow(InventoryLine inventoryLine)
    {
        InitializeComponent();
        _inventoryLine = inventoryLine;
        _scannedCodes = new ObservableCollection<string>(inventoryLine.ScannedCodes);
        lbCodes.ItemsSource = _scannedCodes;

        txtProductName.Text = inventoryLine.Batch?.Product?.Name ?? "Товар не найден";
        txtProductDetails.Text = $"Серия: {inventoryLine.Batch?.Series} | Срок: {inventoryLine.Batch?.ExpirationDate:dd.MM.yyyy}";
        
        UpdateCount();
        txtScan.Focus();
    }

    private void TxtScan_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtScan.Text))
        {
            var code = txtScan.Text.Trim();
            
            if (_scannedCodes.Contains(code))
            {
                MessageBox.Show("Этот код уже был сканирован!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                _scannedCodes.Add(code);
                _undoStack.Push(code);
                btnUndo.IsEnabled = true;
                UpdateCount();
            }
            
            txtScan.Clear();
            txtScan.Focus();
        }
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 0)
        {
            var code = _undoStack.Pop();
            _scannedCodes.Remove(code);
            btnUndo.IsEnabled = _undoStack.Count > 0;
            UpdateCount();
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // Update the original inventory line's scanned codes
        _inventoryLine.ScannedCodes.Clear();
        foreach (var code in _scannedCodes)
        {
            _inventoryLine.ScannedCodes.Add(code);
        }
        
        DialogResult = true;
        Close();
    }

    private void UpdateCount()
    {
        txtCount.Text = _scannedCodes.Count.ToString();
    }
}
