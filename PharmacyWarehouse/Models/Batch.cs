using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace PharmacyWarehouse.Models;

public partial class Batch : ObservableObject
{
    private int _id;
    private int _productId;
    private int _supplierId;
    private string _series = null!;          
    private DateOnly _expirationDate;
    private decimal _purchasePrice;
    private decimal _sellingPrice;
    private int _quantity;
    private DateOnly _arrivalDate;
    private bool _isActive = true;
    private int? _incomingDocumentId;         

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int ProductId
    {
        get => _productId;
        set => SetProperty(ref _productId, value);
    }

    public int SupplierId
    {
        get => _supplierId;
        set => SetProperty(ref _supplierId, value);
    }

    public string Series
    {
        get => _series;
        set => SetProperty(ref _series, value);
    }

    public DateOnly ExpirationDate
    {
        get => _expirationDate;
        set => SetProperty(ref _expirationDate, value);
    }

    public decimal PurchasePrice
    {
        get => _purchasePrice;
        set => SetProperty(ref _purchasePrice, value);
    }

    public decimal SellingPrice
    {
        get => _sellingPrice;
        set => SetProperty(ref _sellingPrice, value);
    }

    public int Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public DateOnly ArrivalDate
    {
        get => _arrivalDate;
        set => SetProperty(ref _arrivalDate, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public int? IncomingDocumentId
    {
        get => _incomingDocumentId;
        set => SetProperty(ref _incomingDocumentId, value);
    }

    public ICollection<DocumentLine> DocumentLines { get; set; } = new ObservableCollection<DocumentLine>();
    public Product Product { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public Document? IncomingDocument { get; set; }  

    [NotMapped]
    public bool IsExpired => ExpirationDate <= DateOnly.FromDateTime(DateTime.Now);

    [NotMapped]
    public bool IsExpiringSoon
    {
        get
        {
            var daysUntilExpiration = ExpirationDate.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
            return daysUntilExpiration > 0 && daysUntilExpiration <= 30;
        }
    }

    [NotMapped]
    public int DaysUntilExpiration => ExpirationDate.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;

    [NotMapped]
    public string ExpirationStatus
    {
        get
        {
            if (IsExpired) return "Просрочен";
            if (IsExpiringSoon) return $"Истекает через {DaysUntilExpiration} дн.";
            return "В норме";
        }
    }

    [NotMapped]
    public Brush ExpirationStatusBrush
    {
        get
        {
            if (IsExpired) return Brushes.Red;
            if (IsExpiringSoon) return Brushes.Orange;
            return Brushes.Green;
        }
    }

    [NotMapped]
    public decimal TotalPurchaseValue => PurchasePrice * Quantity;

    [NotMapped]
    public decimal TotalSellingValue => SellingPrice * Quantity;

    [NotMapped]
    public decimal ProfitMargin => SellingPrice - PurchasePrice;

    [NotMapped]
    public decimal ProfitPercentage => PurchasePrice > 0 ? (ProfitMargin / PurchasePrice) * 100 : 0;

    [NotMapped]
    public bool HasLowQuantity => Quantity < 10;

    [NotMapped]
    public string StatusText
    {
        get
        {
            if (!IsActive) return "Неактивна";
            if (Quantity <= 0) return "Распродана";
            if (IsExpired) return "Просрочена";
            if (IsExpiringSoon) return "Истекает";
            if (HasLowQuantity) return "Мало";
            return "Активна";
        }
    }

    [NotMapped]
    public Brush StatusBrush
    {
        get
        {
            if (!IsActive) return Brushes.Gray;
            if (Quantity <= 0) return Brushes.Purple;
            if (IsExpired) return Brushes.Red;
            if (IsExpiringSoon) return Brushes.Orange;
            if (HasLowQuantity) return Brushes.Yellow;
            return Brushes.Green;
        }
    }
}