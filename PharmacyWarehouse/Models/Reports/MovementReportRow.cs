namespace PharmacyWarehouse.Models.Reports;

public class MovementReportRow
{
    public string ProductName { get; set; } = null!;
    public int IncomingQty { get; set; }
    public int OutgoingQty { get; set; }
    public int WriteOffQty { get; set; }
    public int OpeningBalance { get; set; }
    public int ClosingBalance { get; set; }
}
