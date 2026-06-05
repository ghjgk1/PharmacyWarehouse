namespace PharmacyWarehouse.Models.Reports;

public class SalesAnalysisRow
{
    public string ProductName { get; set; } = null!;
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AveragePricePerUnit => TotalQuantitySold > 0 ? TotalRevenue / TotalQuantitySold : 0;
    public int Rank { get; set; }
}
