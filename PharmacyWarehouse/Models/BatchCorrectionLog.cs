using System;

namespace PharmacyWarehouse.Models;

public class BatchCorrectionLog : ObservableObject
{
    private int _id;
    private int _batchId;
    private int _correctionDocumentId;
    private DateTime _correctionDate;
    private string _fieldName = null!;
    private string _oldValue = null!;
    private string _newValue = null!;
    private string _changedBy = null!;
    private string _reason = null!;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int BatchId
    {
        get => _batchId;
        set => SetProperty(ref _batchId, value);
    }

    public int CorrectionDocumentId
    {
        get => _correctionDocumentId;
        set => SetProperty(ref _correctionDocumentId, value);
    }

    public DateTime CorrectionDate
    {
        get => _correctionDate;
        set => SetProperty(ref _correctionDate, value);
    }

    public string FieldName
    {
        get => _fieldName;
        set => SetProperty(ref _fieldName, value);
    }

    public string OldValue
    {
        get => _oldValue;
        set => SetProperty(ref _oldValue, value);
    }

    public string NewValue
    {
        get => _newValue;
        set => SetProperty(ref _newValue, value);
    }

    public string ChangedBy
    {
        get => _changedBy;
        set => SetProperty(ref _changedBy, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public virtual Batch Batch { get; set; } = null!;
    public virtual Document CorrectionDocument { get; set; } = null!;
}