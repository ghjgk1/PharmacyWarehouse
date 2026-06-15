using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.DocumentGeneration;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PharmacyWarehouse.Windows;

public partial class InventoryEditWindow : Window
{
    private readonly InventoryService _inventoryService;
    private readonly IExcelReportGenerator _excelGenerator;
    private readonly Inventory _inventory;
    private ObservableCollection<InventoryLine> _allLines;
    private ObservableCollection<InventoryLine> _filteredLines;

    public InventoryEditWindow(Inventory inventory)
    {
        InitializeComponent();
        _inventoryService = App.ServiceProvider.GetService<InventoryService>()!;
        _excelGenerator = App.ServiceProvider.GetService<IExcelReportGenerator>()!;
        _inventory = inventory;
        _allLines = new ObservableCollection<InventoryLine>(inventory.InventoryLines);
        _filteredLines = new ObservableCollection<InventoryLine>(_allLines);

        // Подписываемся на PropertyChanged для всех строк
        foreach (var line in _allLines)
        {
            line.PropertyChanged += InventoryLine_PropertyChanged;
        }

        txtTitle.Text = $"Акт инвентаризации №{inventory.Number}";
        txtSubtitle.Text = $"Дата: {inventory.InventoryDate:dd.MM.yyyy} | Статус: {inventory.Status}";
        dgLines.ItemsSource = _filteredLines;

        if (inventory.Status == "Completed")
        {
            // Полностью отключаем редактирование для завершенных инвентаризаций
            btnComplete.IsEnabled = false;
            dgLines.IsReadOnly = true;
            
            // Скрываем кнопки сохранения и завершения, оставляем только экспорт
            var saveButton = ((StackPanel)btnComplete.Parent).Children.OfType<Button>().FirstOrDefault(b => b.Content.ToString() == "Сохранить");
            if (saveButton != null)
                saveButton.Visibility = Visibility.Collapsed;
            
            btnComplete.Visibility = Visibility.Collapsed;
        }

        UpdateProgress();
    }
    
    private void InventoryLine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InventoryLine.IsVerified) || 
            e.PropertyName == nameof(InventoryLine.ActualQuantity))
        {
            UpdateProgress();
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void Filters_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = txtSearch.Text?.ToLower() ?? "";
        var onlyUnverified = chkOnlyUnverified.IsChecked ?? false;
        var onlyDifferences = chkOnlyDifferences.IsChecked ?? false;

        var filtered = _allLines.Where(line =>
        {
            var matchesSearch = string.IsNullOrEmpty(search) ||
                               (line.Batch?.Product?.Name?.ToLower().Contains(search) ?? false) ||
                               (line.Batch?.Series?.ToLower().Contains(search) ?? false) ||
                               (line.Batch?.Product?.Manufacturer?.ToLower().Contains(search) ?? false);

            var matchesUnverified = !onlyUnverified || !line.IsVerified;
            var matchesDifference = !onlyDifferences || line.Difference != 0;

            return matchesSearch && matchesUnverified && matchesDifference;
        }).ToList();

        _filteredLines.Clear();
        foreach (var line in filtered)
        {
            _filteredLines.Add(line);
        }
    }

    private void UpdateProgress()
    {
        var total = _allLines.Count;
        var verified = _allLines.Count(l => l.IsVerified);
        var remaining = total - verified;

        txtProgress.Text = $"Проверено: {verified} из {total}";
        progressBar.Maximum = total;
        progressBar.Value = verified;

        if (remaining > 0)
        {
            txtCompleteHint.Text = $"Осталось проверить: {remaining}";
            btnComplete.IsEnabled = false;
            btnComplete.ToolTip = $"Осталось проверить {remaining} позиций";
        }
        else
        {
            txtCompleteHint.Text = "Все позиции проверены";
            btnComplete.IsEnabled = _inventory.Status != "Completed";
            btnComplete.ToolTip = null;
        }
    }

    private void BtnPlus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: InventoryLine line })
        {
            line.ActualQuantity = (line.ActualQuantity ?? 0) + 1;
            UpdateProgress();
        }
    }

    private void BtnMinus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: InventoryLine line })
        {
            var newValue = (line.ActualQuantity ?? 0) - 1;
            if (newValue >= 0)
            {
                line.ActualQuantity = newValue;
                UpdateProgress();
            }
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: InventoryLine line })
        {
            line.ActualQuantity = 0;
            UpdateProgress();
        }
    }

    private void TxtActual_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !char.IsDigit(e.Text[0]);
    }

    private void TxtActual_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateProgress();
    }

    private void TxtActual_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && e.Key == Key.Enter)
        {
            if (int.TryParse(textBox.Text, out var value) && value < 0)
            {
                textBox.Text = "0";
            }
            UpdateProgress();
        }
    }

    private void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: InventoryLine line })
        {
            var scanWindow = new MdlpScanWindow(line);
            scanWindow.Owner = this;
            if (scanWindow.ShowDialog() == true)
            {
                line.ActualQuantity = line.ScannedCodes.Count;
                UpdateProgress();
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _inventoryService.SaveActualQuantities(_inventory.Id, _allLines.ToList());
            MessageBox.Show("Данные сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Complete_Click(object sender, RoutedEventArgs e)
    {
        // Step 4: Check if there are any differences first
        var hasDifferences = _allLines.Any(l => l.Difference != 0);
        if (!hasDifferences)
        {
            MessageBox.Show(
                "Расхождений не обнаружено. Корректирующие документы не будут созданы.", 
                "Информация", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
            
            // Still complete the inventory even if no differences?
            // Wait let's check the plan: "если ни одна строка не имеет Difference !=0, показать пользователю сообщение что расхождений нет и документы создаваться не будут"
            // So let's still complete the inventory but tell user no docs were created
            try
            {
                _inventoryService.SaveActualQuantities(_inventory.Id, _allLines.ToList());
                await _inventoryService.CompleteInventoryAsync(_inventory.Id);
                MessageBox.Show("Инвентаризация завершена. Расхождений не обнаружено.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        var result = MessageBox.Show(
            "Завершить инвентаризацию? Будут созданы корректирующие документы для расхождений.",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _inventoryService.SaveActualQuantities(_inventory.Id, _allLines.ToList());
            var (writeOffCreated, receiptCreated, writeOffLines, receiptLines) = await _inventoryService.CompleteInventoryAsync(_inventory.Id);
            
            // Step 3: Show message with details
            var messageParts = new List<string>();
            messageParts.Add("Инвентаризация завершена.");
            
            if (writeOffCreated)
                messageParts.Add($"Создан акт списания ({writeOffLines} позиций).");
            if (receiptCreated)
                messageParts.Add($"Создана приходная накладная ({receiptLines} позиций).");
            if (!writeOffCreated && !receiptCreated)
                messageParts.Add("Расхождений не обнаружено — документы не созданы.");

            var message = string.Join("\n", messageParts);
            
            MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel файлы (*.xlsx)|*.xlsx",
            FileName = $"Инвентаризация_{_inventory.Number}.xlsx"
        };

        if (dialog.ShowDialog() == true)
            _excelGenerator.GenerateInventoryReport(_inventory, _allLines.ToList(), dialog.FileName);
    }
}
