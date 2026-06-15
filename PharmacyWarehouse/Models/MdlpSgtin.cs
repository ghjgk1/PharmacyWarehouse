using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace PharmacyWarehouse.Models;

public partial class MdlpSgtin : ObservableObject
{
    private int _id;
    private int? _productId;
    private int? _batchId;
    private string _sgtin = null!;
    private string? _gtin;
    private string? _serialNumber;
    private string _status = null!;
    private DateTime _createdAt;
    private DateTime? _statusChangedAt;
    private string? _previousStatus;
    private int? _mdlpDocumentId;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int? ProductId
    {
        get => _productId;
        set => SetProperty(ref _productId, value);
    }

    public int? BatchId
    {
        get => _batchId;
        set => SetProperty(ref _batchId, value);
    }

    public string Sgtin
    {
        get => _sgtin;
        set => SetProperty(ref _sgtin, value);
    }

    [NotMapped]
    public string? Gtin
    {
        get => _gtin;
        set => SetProperty(ref _gtin, value);
    }

    [NotMapped]
    public string? SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime? StatusChangedAt
    {
        get => _statusChangedAt;
        set => SetProperty(ref _statusChangedAt, value);
    }

    public string? PreviousStatus
    {
        get => _previousStatus;
        set => SetProperty(ref _previousStatus, value);
    }

    public int? MdlpDocumentId
    {
        get => _mdlpDocumentId;
        set => SetProperty(ref _mdlpDocumentId, value);
    }

    [NotMapped]
    public virtual Product? Product { get; set; }
    [NotMapped]
    public virtual Batch? Batch { get; set; }

    public virtual MdlpDocument? MdlpDocument { get; set; }

    [NotMapped]
    public string StatusText
    {
        get
        {
            return Status switch
            {
                "InStock" => "На складе",
                "InCirculation" => "В обороте",
                "Sold" => "Продан",
                "Shipped" => "Отгружено",
                "RetailSold" => "Продан в розницу",
                "WrittenOff" => "Списан",
                "Withdrawn" => "Изъят",
                _ => Status
            };
        }
    }

    [NotMapped]
    public Brush StatusColor
    {
        get
        {
            return Status switch
            {
                "InStock" => Brushes.Blue,
                "InCirculation" => Brushes.Green,
                "Sold" => Brushes.Orange,
                "Shipped" => Brushes.Purple,
                "RetailSold" => Brushes.Orange,
                "WrittenOff" => Brushes.Red,
                "Withdrawn" => Brushes.DarkRed,
                _ => Brushes.Gray
            };
        }
    }
}
