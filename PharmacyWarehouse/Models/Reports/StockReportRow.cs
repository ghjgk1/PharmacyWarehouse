namespace PharmacyWarehouse.Models.Reports;

public class StockReportRow
{
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string Manufacturer { get; set; } = null!;
    public string Series { get; set; } = null!;
    public DateOnly? ExpirationDate { get; set; }
    public int Quantity { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TotalValue => Quantity * SellingPrice;
}
