using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace PharmacyWarehouse.Models;

public partial class Product : ObservableObject
{
    private int _id;
    private string _name = null!;
    private int? _categoryId;
    private string? _releaseForm;
    private string _manufacturer = String.Empty;
    private bool _requiresPrescription = false;
    private string _unitOfMeasure = "шт.";
    private int _minRemainder = 10;
    private bool _isActive = true;  
    private bool _isSalesBlocked = false;
    private string? _archiveReason;      
    private string? _archiveComment;     
    private DateTime? _archiveDate;
    private string? _description;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int? CategoryId
    {
        get => _categoryId;
        set => SetProperty(ref _categoryId, value);
    }

    public string? ReleaseForm
    {
        get => _releaseForm;
        set => SetProperty(ref _releaseForm, value);
    }

    public string Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value);
    }

    public bool RequiresPrescription
    {
        get => _requiresPrescription;
        set => SetProperty(ref _requiresPrescription, value);
    }

    public string UnitOfMeasure
    {
        get => _unitOfMeasure;
        set => SetProperty(ref _unitOfMeasure, value);
    }

    public int MinRemainder
    {
        get => _minRemainder;
        set => SetProperty(ref _minRemainder, value);
    }
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsSalesBlocked
    {
        get => _isSalesBlocked;
        set => SetProperty(ref _isSalesBlocked, value);
    }

    public DateTime? ArchiveDate
    {
        get => _archiveDate;
        set => SetProperty(ref _archiveDate, value);
    }

    public string? ArchiveReason
    {
        get => _archiveReason;
        set => SetProperty(ref _archiveReason, value);
    }

    public string? ArchiveComment
    {
        get => _archiveComment;
        set => SetProperty(ref _archiveComment, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public virtual ICollection<Batch> Batches { get; set; } = new ObservableCollection<Batch>();
    public virtual Category? Category { get; set; }
    public virtual ICollection<DocumentLine> DocumentLines { get; set; } = new ObservableCollection<DocumentLine>();
    

    [NotMapped]
    public bool IsLowStock => CurrentStock > 0 && CurrentStock <= MinRemainder;

    [NotMapped]
    public bool IsOutOfStock => CurrentStock <= 0; 
    
    [NotMapped]
    public bool IsInStock => CurrentStock > 0;

    [NotMapped]
    public decimal CurrentStock => Batches?.Where(b => b.IsActive).Sum(b => b.Quantity) ?? 0;

   
    [NotMapped]
    public DateOnly? NearestExpirationDate
    {
        get
        {
            if (Batches == null || !Batches.Any(b => b.IsActive && b.Quantity > 0))
                return null;

            return Batches
                .Where(b => b.IsActive && b.Quantity > 0)
                .Min(b => b.ExpirationDate);
        }
    }

    [NotMapped]
    public bool HasExpiringBatches
    {
        get
        {
            if (Batches == null) return false;

            var thirtyDaysFromNow = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
            var today = DateOnly.FromDateTime(DateTime.Now);

            return Batches.Any(b =>
                b.IsActive &&
                b.Quantity > 0 &&
                b.ExpirationDate <= thirtyDaysFromNow &&
                b.ExpirationDate >= today);
        }
    }

    [NotMapped]
    public bool HasExpiredBatches
    {
        get
        {
            if (Batches == null) return false;

            var today = DateOnly.FromDateTime(DateTime.Now);
            return Batches.Any(b =>
                b.IsActive &&
                b.Quantity > 0 &&
                b.ExpirationDate < today);
        }
    }

    [NotMapped]
    public decimal TotalValue => Batches?
        .Where(b => b.IsActive)
        .Sum(b => b.TotalPurchaseValue) ?? 0;

    [NotMapped]
    public bool CanBeOrdered => IsActive; 

    [NotMapped]
    public bool CanBeSold => !IsSalesBlocked; 

    [NotMapped]
    public string ArchiveStatus
    {
        get
        {
            if (IsActive) return "Активный";
            if (IsSalesBlocked) return "Заблокирован";
            return "Дораспродажа";  
        }
    }

    [NotMapped]
    public Brush StatusBrush
    {
        get
        {
            if (IsActive)
            {
                if (IsOutOfStock) return Brushes.Orange;
                if (IsLowStock) return Brushes.Gold;
                return Brushes.Green;
            }
            return IsSalesBlocked ? Brushes.Red : Brushes.Gray;
        }
    }
}