namespace PharmacyWarehouse.Models.Reports;

public class InventoryReportRow
{
    public string ProductName { get; set; } = null!;
    public string Series { get; set; } = null!;
    public DateOnly ExpirationDate { get; set; }
    public int AccountedQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int Difference { get; set; }
    public string DifferenceStatus => Difference == 0 ? "Совпадает"
                                    : Difference > 0 ? "Излишек"
                                    : "Недостача";
}
