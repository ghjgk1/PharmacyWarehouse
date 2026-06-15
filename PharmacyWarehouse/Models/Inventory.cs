using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWarehouse.Models;

public partial class Inventory : ObservableObject
{
    private int _id;
    private string _number = null!;
    private DateOnly _inventoryDate;
    private string _status = "Draft";
    private string _createdBy = null!;
    private DateTime _createdAt;
    private DateTime? _completedAt;
    private string? _notes;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    public DateOnly InventoryDate
    {
        get => _inventoryDate;
        set => SetProperty(ref _inventoryDate, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string CreatedBy
    {
        get => _createdBy;
        set => SetProperty(ref _createdBy, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set => SetProperty(ref _completedAt, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public virtual ICollection<InventoryLine> InventoryLines { get; set; } = new ObservableCollection<InventoryLine>();

    [NotMapped]
    public string DisplayText => $"{Number} ({InventoryDate:dd.MM.yyyy})";

    [NotMapped]
    public string DisplayStatus => Status switch
    {
        "Draft" => "Черновик",
        "Completed" => "Завершен",
        _ => Status
    };
}
