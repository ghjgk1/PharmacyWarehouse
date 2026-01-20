using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWarehouse.Models;

public partial class DocumentLine : ObservableObject
{
    private int _id;
    private int _documentId;
    private int _productId;
    private string _series;
    private DateOnly? _expirationDate;
    private int _quantity;
    private decimal _unitPrice;
    private decimal? _sellingPrice;
    private string? _notes;

    // Для прихода
    private int? _createdBatchId;

    // Для расхода/списания
    private int? _sourceBatchId;

    // Для корректировки
    private string? _oldValue;        
    private string? _newValue;        
    private string? _correctionNotes; 

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int DocumentId
    {
        get => _documentId;
        set => SetProperty(ref _documentId, value);
    }

    public int ProductId
    {
        get => _productId;
        set => SetProperty(ref _productId, value);
    }

    public string Series
    {
        get => _series;
        set => SetProperty(ref _series, value);
    }

    public DateOnly? ExpirationDate
    {
        get => _expirationDate;
        set => SetProperty(ref _expirationDate, value);
    }

    public int Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public decimal UnitPrice
    {
        get => _unitPrice;
        set => SetProperty(ref _unitPrice, value);
    }

    public decimal? SellingPrice
    {
        get => _sellingPrice;
        set => SetProperty(ref _sellingPrice, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }
    // Для прихода
    public int? CreatedBatchId
    {
        get => _createdBatchId;
        set => SetProperty(ref _createdBatchId, value);
    }
    // Для списания/расхода
    public int? SourceBatchId
    {
        get => _sourceBatchId;
        set => SetProperty(ref _sourceBatchId, value);
    }

    // Для корректировки
    public string? OldValue
    {
        get => _oldValue;
        set => SetProperty(ref _oldValue, value);
    }

    public string? NewValue
    {
        get => _newValue;
        set => SetProperty(ref _newValue, value);
    }

    public string? CorrectionNotes
    {
        get => _correctionNotes;
        set => SetProperty(ref _correctionNotes, value);
    }

    public decimal TotalPrice => Quantity * UnitPrice;

    public virtual Document Document { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual Batch? CreatedBatch { get; set; }
    public virtual Batch? SourceBatch { get; set; }

    [NotMapped]
    public bool IsExpiringSoon
    {
        get
        {
            if (!ExpirationDate.HasValue) return false;
            return ExpirationDate.Value.ToDateTime(TimeOnly.MinValue) <= DateTime.Now.AddDays(30);
        }
    }
}