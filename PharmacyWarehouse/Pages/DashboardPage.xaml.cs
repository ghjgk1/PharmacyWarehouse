using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models.Reports;
using PharmacyWarehouse.Services;
using System.Windows.Controls;

namespace PharmacyWarehouse.Pages;

public partial class DashboardPage : Page
{
    private readonly DashboardService _dashboardService;

    public DashboardPage()
    {
        InitializeComponent();
        _dashboardService = App.ServiceProvider.GetService<DashboardService>()!;
        Loaded += DashboardPage_Loaded;
    }

    private void DashboardPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var stats = _dashboardService.GetStats();
        txtTotalProducts.Text = stats.TotalActiveProducts.ToString();
        txtTotalBatches.Text = stats.TotalActiveBatches.ToString();
        txtExpiredBatches.Text = stats.ExpiredBatchesCount.ToString();
        txtLowStock.Text = stats.LowStockCount.ToString();
        txtExpiringBatches.Text = stats.ExpiringBatchesCount.ToString();

        var topSales = _dashboardService.GetTopSales(30, 5)
            .Select((item, index) => new { item.ProductName, item.TotalQuantity, item.TotalRevenue, Rank = index + 1 })
            .ToList();
        dgTopSales.ItemsSource = topSales;

        var dynamics = _dashboardService.GetStockDynamics(30);
        var maxValue = dynamics.Max(d => Math.Max(d.IncomeQty, Math.Max(d.SaleQty, d.WriteOffQty)));
        if (maxValue == 0) maxValue = 1;
        const double maxHeight = 150;

        chartDynamics.ItemsSource = dynamics.Select(d => new ChartBarItem
        {
            DateLabel = d.Date.ToString("dd.MM"),
            IncomeHeight = d.IncomeQty * maxHeight / maxValue,
            IncomeQty = d.IncomeQty,
            SaleHeight = d.SaleQty * maxHeight / maxValue,
            SaleQty = d.SaleQty,
            WriteOffHeight = d.WriteOffQty * maxHeight / maxValue,
            WriteOffQty = d.WriteOffQty
        }).ToList();
    }

    private class ChartBarItem
    {
        public string DateLabel { get; set; } = null!;
        public double IncomeHeight { get; set; }
        public int IncomeQty { get; set; }
        public double SaleHeight { get; set; }
        public int SaleQty { get; set; }
        public double WriteOffHeight { get; set; }
        public int WriteOffQty { get; set; }
    }
}
