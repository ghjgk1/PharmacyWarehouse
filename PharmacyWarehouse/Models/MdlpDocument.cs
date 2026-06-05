using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace PharmacyWarehouse.Models;

public partial class MdlpDocument : ObservableObject
{
    private int _id;
    private int _documentId;
    private string? _mdlpDocumentId;
    private string _operationType = null!;
    private string _status = null!;
    private string? _requestXml;
    private string? _responseXml;
    private DateTime? _sentAt;
    private DateTime? _processedAt;
    private string? _errorMessage;
    private DateTime _createdAt;
    private string? _schemaVersion;
    private string? _ticket;
    private string? _errorCode;
    private string? _errorDetails;
    private int _retryCount;

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

    public string? MdlpDocumentId
    {
        get => _mdlpDocumentId;
        set => SetProperty(ref _mdlpDocumentId, value);
    }

    public string OperationType
    {
        get => _operationType;
        set => SetProperty(ref _operationType, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string? RequestXml
    {
        get => _requestXml;
        set => SetProperty(ref _requestXml, value);
    }

    public string? ResponseXml
    {
        get => _responseXml;
        set => SetProperty(ref _responseXml, value);
    }

    public DateTime? SentAt
    {
        get => _sentAt;
        set => SetProperty(ref _sentAt, value);
    }

    public DateTime? ProcessedAt
    {
        get => _processedAt;
        set => SetProperty(ref _processedAt, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public string? SchemaVersion
    {
        get => _schemaVersion;
        set => SetProperty(ref _schemaVersion, value);
    }

    public string? Ticket
    {
        get => _ticket;
        set => SetProperty(ref _ticket, value);
    }

    public string? ErrorCode
    {
        get => _errorCode;
        set => SetProperty(ref _errorCode, value);
    }

    public string? ErrorDetails
    {
        get => _errorDetails;
        set => SetProperty(ref _errorDetails, value);
    }

    public int RetryCount
    {
        get => _retryCount;
        set => SetProperty(ref _retryCount, value);
    }

    public virtual Document Document { get; set; } = null!;

    public virtual ICollection<MdlpDocumentHistory> MdlpDocumentHistories { get; set; } = new List<MdlpDocumentHistory>();

    public virtual ICollection<MdlpSgtin> MdlpSgtins { get; set; } = new List<MdlpSgtin>();

    [NotMapped]
    public string StatusText
    {
        get
        {
            return Status switch
            {
                "Pending" => "Ожидает отправки",
                "Sent" => "Отправлено",
                "Processing" => "Обрабатывается",
                "Success" => "Успешно",
                "Accepted" => "Принято",
                "Rejected" => "Отклонено",
                "Error" => "Ошибка",
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
                "Pending" => Brushes.Orange,
                "Sent" => Brushes.Blue,
                "Processing" => Brushes.LightBlue,
                "Success" => Brushes.Green,
                "Accepted" => Brushes.Green,
                "Rejected" => Brushes.Red,
                "Error" => Brushes.Red,
                _ => Brushes.Gray
            };
        }
    }

    [NotMapped]
    public string OperationTypeText
    {
        get
        {
            return OperationType switch
            {
                "Income" => "Приёмка",
                "Outcome" => "Реализация",
                "WriteOff" => "Списание",
                _ => OperationType
            };
        }
    }

    [NotMapped]
    public bool IsError => Status == "Error";

    [NotMapped]
    public bool CanRetry
    {
        get
        {
            return Status == "Error" && RetryCount < (MdlpSetting?.MaxRetries ?? 3);
        }
    }

    [NotMapped]
    public MdlpSetting? MdlpSetting { get; set; }
}
