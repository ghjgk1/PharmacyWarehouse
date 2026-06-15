using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.DocumentGeneration;
using PharmacyWarehouse.Windows;
using System.Windows;
using System.Windows.Controls;

namespace PharmacyWarehouse.Pages;

public partial class InventoryPage : Page
{
    private readonly InventoryService _inventoryService;
    private readonly IExcelReportGenerator _excelGenerator;

    public InventoryPage()
    {
        InitializeComponent();
        _inventoryService = App.ServiceProvider.GetService<InventoryService>()!;
        _excelGenerator = App.ServiceProvider.GetService<IExcelReportGenerator>()!;
        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        dgInventories.ItemsSource = _inventoryService.GetAll();
    }

    private void CreateInventory_Click(object sender, RoutedEventArgs e)
    {
        var currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";
        var inventory = _inventoryService.CreateInventory(DateOnly.FromDateTime(DateTime.Today), currentUser);
        var window = new InventoryEditWindow(inventory);
        window.Owner = Window.GetWindow(this);
        if (window.ShowDialog() == true)
            LoadData();
    }

    private void OpenInventory_Click(object sender, RoutedEventArgs e)
    {
        if (dgInventories.SelectedItem is not Inventory selected)
        {
            MessageBox.Show("Выберите акт инвентаризации", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var inventory = _inventoryService.GetWithLines(selected.Id);
        if (inventory == null) return;

        var window = new InventoryEditWindow(inventory);
        window.Owner = Window.GetWindow(this);
        if (window.ShowDialog() == true)
            LoadData();
    }

    private void ExportInventory_Click(object sender, RoutedEventArgs e)
    {
        if (dgInventories.SelectedItem is not Inventory selected)
        {
            MessageBox.Show("Выберите акт для экспорта", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var inventory = _inventoryService.GetWithLines(selected.Id);
        if (inventory == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Excel файлы (*.xlsx)|*.xlsx",
            FileName = $"Инвентаризация_{inventory.Number}.xlsx"
        };

        if (dialog.ShowDialog() == true)
            _excelGenerator.GenerateInventoryReport(inventory, inventory.InventoryLines.ToList(), dialog.FileName);
    }

    private void DeleteInventory_Click(object sender, RoutedEventArgs e)
    {
        if (dgInventories.SelectedItem is not Inventory selected)
        {
            MessageBox.Show("Выберите акт для удаления", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Удалить акт инвентаризации №{selected.Number}?",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _inventoryService.DeleteInventory(selected.Id);
            MessageBox.Show("Акт инвентаризации удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DgInventories_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgInventories.SelectedItem is not Inventory selected) return;
        
        var inventory = _inventoryService.GetWithLines(selected.Id);
        if (inventory == null) return;

        var window = new InventoryEditWindow(inventory);
        window.Owner = Window.GetWindow(this);
        if (window.ShowDialog() == true)
            LoadData();
    }
}
