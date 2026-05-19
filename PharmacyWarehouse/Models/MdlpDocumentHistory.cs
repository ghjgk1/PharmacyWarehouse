using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace PharmacyWarehouse.Models;

public partial class MdlpDocumentHistory : ObservableObject
{
    private int _id;
    private int _mdlpDocumentId;
    private string _status = null!;
    private DateTime _changedAt;
    private string? _comment;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int MdlpDocumentId
    {
        get => _mdlpDocumentId;
        set => SetProperty(ref _mdlpDocumentId, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DateTime ChangedAt
    {
        get => _changedAt;
        set => SetProperty(ref _changedAt, value);
    }

    public string? Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public virtual MdlpDocument MdlpDocument { get; set; } = null!;

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
}
