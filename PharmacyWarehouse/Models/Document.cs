using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.ObjectModel;

namespace PharmacyWarehouse.Models;

public enum DocumentType
{
    Incoming,     // Приходная накладная
    Outgoing,     // Расходная накладная  
    WriteOff,     // Акт списания
    Correction,   // Корректировочный документ
}

public enum DocumentStatus
{
    Draft,        // Черновик
    Processed,    // Проведен
    Blocked       // Заблокирован
}

public enum WriteOffReason
{
    Expired,                 // Просрочка
    Damaged,                 // Бой/повреждение
    Defect,                  // Брак
    Lost,                    // Потеря
    Recall,                  // Отзыв Росздравнадзора
    SaleBan,                 // Запрет на реализацию
    StorageViolation,        // Нарушение условий хранения
    Utilization,             // Утилизация
    InventoryDifference,     // Инвентаризационная разница
    Other                    // Иное
}

public enum CorrectionType
{
    Quantity,        // Корректировка количества
    Price,           // Корректировка цены
}

public partial class Document : ObservableObject
{
    private int _id;
    private string _number = null!;
    private DocumentType _type;
    private DateTime _date;
    private DocumentStatus _status = DocumentStatus.Draft;

    // Общее
    private string? _notes;
    private string _createdBy = null!;
    private DateTime _createdAt;
    private string? _signedBy;
    private DateTime? _signedAt;

    // Приходная 
    private int? _supplierId;
    private string? _supplierInvoiceNumber;
    private DateTime? _supplierInvoiceDate;

    // Расходная
    private string? _customerName;
    private string? _customerDocument;
    private decimal? _amount;


    // Списание
    private WriteOffReason? _writeOffReason;
    private string? _writeOffCommission;
    
    
    // Корректировка
    private int? _originalDocumentId;
    private Document? _originalDocument;
    private CorrectionType? _correctionType; 
    private string? _correctionReason;     

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

    public DocumentType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public DocumentStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // Общее
    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
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

    public string? SignedBy
    {
        get => _signedBy;
        set => SetProperty(ref _signedBy, value);
    }

    public DateTime? SignedAt
    {
        get => _signedAt;
        set => SetProperty(ref _signedAt, value);
    }

    public decimal? Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }


    // Приходная 
    public int? SupplierId
    {
        get => _supplierId;
        set => SetProperty(ref _supplierId, value);
    }

    public string? SupplierInvoiceNumber
    {
        get => _supplierInvoiceNumber;
        set => SetProperty(ref _supplierInvoiceNumber, value);
    }

    public DateTime? SupplierInvoiceDate
    {
        get => _supplierInvoiceDate;
        set => SetProperty(ref _supplierInvoiceDate, value);
    }

    // Расходная
    public string? CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string? CustomerDocument
    {
        get => _customerDocument;
        set => SetProperty(ref _customerDocument, value);
    }

    // Списание
    public WriteOffReason? WriteOffReason
    {
        get => _writeOffReason;
        set => SetProperty(ref _writeOffReason, value);
    }

    public string? WriteOffCommission
    {
        get => _writeOffCommission;
        set => SetProperty(ref _writeOffCommission, value);
    }

    //Корректировка

    public int? OriginalDocumentId
    {
        get => _originalDocumentId;
        set => SetProperty(ref _originalDocumentId, value);
    }
    public Document? OriginalDocument
    {
        get => _originalDocument;
        set => SetProperty(ref _originalDocument, value);
    }

    public CorrectionType? CorrectionType
    {
        get => _correctionType;
        set => SetProperty(ref _correctionType, value);
    }

    public string? CorrectionReason
    {
        get => _correctionReason;
        set => SetProperty(ref _correctionReason, value);
    }

    [NotMapped]
    public decimal TotalAmount => Amount ?? 0;

    [NotMapped]
    public string TypeDescription => Type switch
    {
        DocumentType.Incoming => "Приходная накладная",
        DocumentType.Outgoing => "Расходная накладная",
        DocumentType.WriteOff => "Акт списания",
        _ => "Неизвестный тип"
    };

    public virtual ICollection<DocumentLine> DocumentLines { get; set; } = new ObservableCollection<DocumentLine>();
    public virtual Supplier? Supplier { get; set; }
    public virtual ICollection<Document> Corrections { get; set; } = new ObservableCollection<Document>();
    public virtual ICollection<Batch> Batches { get; set; } = new ObservableCollection<Batch>();
}