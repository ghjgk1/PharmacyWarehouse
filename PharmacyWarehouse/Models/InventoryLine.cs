using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWarehouse.Models;

public partial class InventoryLine : ObservableObject
{
    private int _id;
    private int _inventoryId;
    private int _batchId;
    private int _expectedQuantity;
    private int? _actualQuantity;
    private string? _notes;
    private ObservableCollection<string> _scannedCodes = new();
    private string? _lastScannedCode;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int InventoryId
    {
        get => _inventoryId;
        set => SetProperty(ref _inventoryId, value);
    }

    public int BatchId
    {
        get => _batchId;
        set => SetProperty(ref _batchId, value);
    }

    public int ExpectedQuantity
    {
        get => _expectedQuantity;
        set => SetProperty(ref _expectedQuantity, value);
    }

    public int? ActualQuantity
    {
        get => _actualQuantity;
        set
        {
            if (SetProperty(ref _actualQuantity, value))
            {
                OnPropertyChanged(nameof(Difference));
                OnPropertyChanged(nameof(DifferenceType));
                OnPropertyChanged(nameof(IsVerified));
            }
        }
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    [NotMapped]
    public ObservableCollection<string> ScannedCodes
    {
        get => _scannedCodes;
        set => SetProperty(ref _scannedCodes, value);
    }

    [NotMapped]
    public string? LastScannedCode
    {
        get => _lastScannedCode;
        set => SetProperty(ref _lastScannedCode, value);
    }

    [NotMapped]
    public int Difference => (ActualQuantity ?? 0) - ExpectedQuantity;

    [NotMapped]
    public string DifferenceType
    {
        get
        {
            var diff = Difference;
            if (diff < 0) return "Shortage";
            if (diff > 0) return "Surplus";
            return "None";
        }
    }

    [NotMapped]
    public bool IsVerified => ActualQuantity.HasValue;

    public virtual Inventory Inventory { get; set; } = null!;
    public virtual Batch Batch { get; set; } = null!;
}
