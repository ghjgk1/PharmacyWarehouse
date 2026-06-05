namespace PharmacyWarehouse.Models.Reports;

public class DashboardStats
{
    public int TotalActiveProducts { get; set; }
    public int TotalActiveBatches { get; set; }
    public int ExpiredBatchesCount { get; set; }
    public int LowStockCount { get; set; }
    public int ExpiringBatchesCount { get; set; }
}

public class TopSaleItem
{
    public string ProductName { get; set; } = null!;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class StockDynamicsPoint
{
    public DateOnly Date { get; set; }
    public int IncomeQty { get; set; }
    public int SaleQty { get; set; }
    public int WriteOffQty { get; set; }
}
