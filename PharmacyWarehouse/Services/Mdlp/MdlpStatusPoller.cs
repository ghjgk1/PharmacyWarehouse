using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.Mdlp;

public class MdlpStatusPoller
{
    private readonly PharmacyWarehouseContext _context;
    private readonly DispatcherTimer _timer;
    private readonly Random _random;

    public event EventHandler<MdlpDocumentStatusChangedEventArgs>? StatusChanged;

    public MdlpStatusPoller(PharmacyWarehouseContext context)
    {
        _context = context;
        _random = new Random();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        await CheckPendingDocumentsAsync();
    }

    private async Task CheckPendingDocumentsAsync()
    {
        var settings = await _context.MdlpSettings.FirstOrDefaultAsync() ?? new MdlpSetting
        {
            SimulatedDelaySeconds = 5,
            UseMock = true
        };

        if (!settings.UseMock)
            return;

        var pendingDocs = await _context.MdlpDocuments
            .Include(d => d.Document)
            .Where(d => d.Status == "Pending" || d.Status == "Processing")
            .ToListAsync();

        foreach (var doc in pendingDocs)
        {
            if (doc.SentAt.HasValue)
            {
                var timeSinceSent = DateTime.Now - doc.SentAt.Value;
                if (timeSinceSent.TotalSeconds >= settings.SimulatedDelaySeconds)
                {
                    var oldStatus = doc.Status;
                    var newStatus = _random.NextDouble() < 0.9 ? "Accepted" : "Rejected";
                    doc.Status = newStatus;
                    doc.ProcessedAt = DateTime.Now;

                    var history = new MdlpDocumentHistory
                    {
                        MdlpDocumentId = doc.Id,
                        Status = newStatus,
                        ChangedAt = DateTime.Now,
                        Comment = newStatus == "Accepted" ? "Операция выполнена успешно" : "Операция отклонена"
                    };
                    _context.MdlpDocumentHistories.Add(history);

                    await _context.SaveChangesAsync();

                    StatusChanged?.Invoke(this, new MdlpDocumentStatusChangedEventArgs(doc, oldStatus, newStatus));
                }
            }
        }
    }
}

public class MdlpDocumentStatusChangedEventArgs : EventArgs
{
    public MdlpDocument Document { get; }
    public string OldStatus { get; }
    public string NewStatus { get; }

    public MdlpDocumentStatusChangedEventArgs(MdlpDocument document, string oldStatus, string newStatus)
    {
        Document = document;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}
