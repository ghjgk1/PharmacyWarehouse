namespace PharmacyWarehouse.Models;

public partial class InventoryScannedCode
{
    public int Id { get; set; }
    public int InventoryLineId { get; set; }
    public string Sgtin { get; set; } = string.Empty;

    public virtual InventoryLine InventoryLine { get; set; } = null!;
}
