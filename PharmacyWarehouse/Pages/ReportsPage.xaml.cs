using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Models.Reports;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.DocumentGeneration;
using System.Windows;
using System.Windows.Controls;

namespace PharmacyWarehouse.Pages;

public partial class ReportsPage : Page
{
    private readonly ReportService _reportService;
    private readonly IExcelReportGenerator _excelGenerator;

    private List<StockReportRow> _stockRows = new();
    private List<MovementReportRow> _movementRows = new();
    private List<SalesAnalysisRow> _salesRows = new();
    private List<InventoryReportRow> _inventoryRows = new();
    private Inventory? _selectedInventory;

    public ReportsPage()
    {
        InitializeComponent();
        _reportService = App.ServiceProvider.GetService<ReportService>()!;
        _excelGenerator = App.ServiceProvider.GetService<IExcelReportGenerator>()!;

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        dpStockDate.SelectedDate = today;
        dpMovementFrom.SelectedDate = monthStart;
        dpMovementTo.SelectedDate = today;
        dpSalesFrom.SelectedDate = monthStart;
        dpSalesTo.SelectedDate = today;

        Loaded += (_, _) =>
        {
            var inventories = _reportService.GetCompletedInventories();
            cmbInventory.ItemsSource = inventories;
            if (inventories.Any())
                cmbInventory.SelectedIndex = 0;
        };
    }

    private void GenerateStock_Click(object sender, RoutedEventArgs e)
    {
        if (!dpStockDate.SelectedDate.HasValue) return;
        _stockRows = _reportService.GetStockReport(dpStockDate.SelectedDate.Value);
        dgStock.ItemsSource = _stockRows;
        btnExportStock.IsEnabled = _stockRows.Any();
    }

    private void ExportStock_Click(object sender, RoutedEventArgs e)
    {
        var path = SaveExcelDialog("Остатки");
        if (path != null && dpStockDate.SelectedDate.HasValue)
            _excelGenerator.GenerateStockReport(_stockRows, path, dpStockDate.SelectedDate.Value);
    }

    private void GenerateMovement_Click(object sender, RoutedEventArgs e)
    {
        if (!dpMovementFrom.SelectedDate.HasValue || !dpMovementTo.SelectedDate.HasValue) return;
        _movementRows = _reportService.GetMovementReport(dpMovementFrom.SelectedDate.Value, dpMovementTo.SelectedDate.Value);
        dgMovement.ItemsSource = _movementRows;
        btnExportMovement.IsEnabled = _movementRows.Any();
    }

    private void ExportMovement_Click(object sender, RoutedEventArgs e)
    {
        var path = SaveExcelDialog("Движение");
        if (path != null && dpMovementFrom.SelectedDate.HasValue && dpMovementTo.SelectedDate.HasValue)
            _excelGenerator.GenerateMovementReport(_movementRows, path, dpMovementFrom.SelectedDate.Value, dpMovementTo.SelectedDate.Value);
    }

    private void GenerateSales_Click(object sender, RoutedEventArgs e)
    {
        if (!dpSalesFrom.SelectedDate.HasValue || !dpSalesTo.SelectedDate.HasValue) return;

        int topN = 10;
        if (cmbTopN.SelectedItem is ComboBoxItem item && item.Tag != null)
            int.TryParse(item.Tag.ToString(), out topN);

        _salesRows = _reportService.GetSalesAnalysis(dpSalesFrom.SelectedDate.Value, dpSalesTo.SelectedDate.Value, topN);
        dgSales.ItemsSource = _salesRows;
        btnExportSales.IsEnabled = _salesRows.Any();

        var chartData = _salesRows.Take(10).ToList();
        var maxRevenue = chartData.Any() ? chartData.Max(r => r.TotalRevenue) : 1;
        if (maxRevenue == 0) maxRevenue = 1;
        const double maxHeight = 250;

        chartSales.ItemsSource = chartData.Select(r => new
        {
            BarHeight = (double)r.TotalRevenue / (double)maxRevenue * maxHeight,
            Label = r.ProductName.Length > 10 ? r.ProductName[..10] + "…" : r.ProductName
        }).ToList();
    }

    private void ExportSales_Click(object sender, RoutedEventArgs e)
    {
        var path = SaveExcelDialog("Анализ_продаж");
        if (path != null && dpSalesFrom.SelectedDate.HasValue && dpSalesTo.SelectedDate.HasValue)
            _excelGenerator.GenerateSalesAnalysisReport(_salesRows, path, dpSalesFrom.SelectedDate.Value, dpSalesTo.SelectedDate.Value);
    }

    private void GenerateInventory_Click(object sender, RoutedEventArgs e)
    {
        if (cmbInventory.SelectedItem is not Inventory inventory) return;

        _selectedInventory = inventory;
        _inventoryRows = _reportService.GetInventoryReport(inventory.Id);
        dgInventory.ItemsSource = _inventoryRows;
        btnExportInventory.IsEnabled = _inventoryRows.Any();
    }

    private void ExportInventory_Click(object sender, RoutedEventArgs e)
    {
        var path = SaveExcelDialog("Инвентаризация");
        if (path != null && _selectedInventory != null)
            _excelGenerator.GenerateInventoryAnalysisReport(
                _inventoryRows, path, _selectedInventory.InventoryDate.ToDateTime(TimeOnly.MinValue));
    }

    private static string? SaveExcelDialog(string defaultName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel файлы (*.xlsx)|*.xlsx",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
