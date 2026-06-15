using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services.Mdlp;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PharmacyWarehouse.Windows;

public partial class MdlpScanWindow : Window
{
    private readonly InventoryLine _inventoryLine;
    private readonly int _expectedQuantity;
    private readonly PharmacyWarehouseContext _context;
    private ObservableCollection<string> _scannedCodes;
    private Stack<string> _undoStack = new();

    public MdlpScanWindow(InventoryLine inventoryLine)
    {
        InitializeComponent();
        _inventoryLine = inventoryLine;
        _expectedQuantity = inventoryLine.ExpectedQuantity;
        _context = App.ServiceProvider.GetService<PharmacyWarehouseContext>()!;

        _scannedCodes = new ObservableCollection<string>(inventoryLine.ScannedCodes);
        lbCodes.ItemsSource = _scannedCodes;

        txtProductName.Text = inventoryLine.Batch?.Product?.Name ?? "Товар не найден";
        txtProductDetails.Text = $"Серия: {inventoryLine.Batch?.Series} | Срок: {inventoryLine.Batch?.ExpirationDate:dd.MM.yyyy}";
        
        UpdateCount();
        txtScan.Focus();
    }

    private async void TxtScan_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtScan.Text))
        {
            var code = txtScan.Text.Trim();
            
            // Проверяем, не сканирован ли код уже
            if (_scannedCodes.Contains(code))
            {
                MessageBox.Show("Этот код уже был сканирован!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtScan.Clear();
                txtScan.Focus();
                return;
            }
            
            // Парсим код с помощью DataMatrixParser
            var parseResult = DataMatrixParser.Parse(code);
            if (!parseResult.IsValid)
            {
                MessageBox.Show($"Некорректный код: {parseResult.ErrorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtScan.Clear();
                txtScan.Focus();
                return;
            }

            if (string.IsNullOrEmpty(parseResult.Sgtin))
            {
                MessageBox.Show("Код не содержит SGTIN.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtScan.Clear();
                txtScan.Focus();
                return;
            }
            
            // Ищем SGTIN в базе
            var sgtinObj = await _context.MdlpSgtins
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Sgtin == parseResult.Sgtin);
                
            if (sgtinObj == null)
            {
                MessageBox.Show("SGTIN не найден в базе.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtScan.Clear();
                txtScan.Focus();
                return;
            }
            
            // Проверяем, что SGTIN принадлежит нужной партии и товару
            if (sgtinObj.BatchId != _inventoryLine.BatchId)
            {
                MessageBox.Show($"Этот SGTIN принадлежит другой партии! Текущая партия: {_inventoryLine.Batch?.Series}, SGTIN принадлежит: {sgtinObj.Batch?.Series}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtScan.Clear();
                txtScan.Focus();
                return;
            }
            
            // Проверяем статус SGTIN
            if (sgtinObj.Status != "InCirculation")
            {
                MessageBox.Show($"Статус SGTIN не допускает операцию: {sgtinObj.Status}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtScan.Clear();
                txtScan.Focus();
                return;
            }
            
            // Все проверки пройдены — добавляем код
            _scannedCodes.Add(parseResult.Sgtin);
            _undoStack.Push(parseResult.Sgtin);
            btnUndo.IsEnabled = true;
            UpdateCount();
            
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
        txtCount.Text = $"{_scannedCodes.Count}/{_expectedQuantity}";
    }
}
