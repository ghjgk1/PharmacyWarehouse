using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.Mdlp;

public class MdlpMockService : IMdlpService
{
    private readonly PharmacyWarehouseContext _context;
    private readonly MdlpXmlGenerator _xmlGenerator;
    private readonly MdlpErrorGenerator _errorGenerator;
    private readonly Random _random;

    public MdlpMockService(PharmacyWarehouseContext context, MdlpXmlGenerator xmlGenerator, MdlpErrorGenerator errorGenerator)
    {
        _context = context;
        _xmlGenerator = xmlGenerator;
        _errorGenerator = errorGenerator;
        _random = new Random();
    }

    // Загружает документ с полными связями (строки + товары + поставщик)
    private async Task<Document> LoadFullDocumentAsync(int documentId)
    {
        return await _context.Documents
            .Include(d => d.Supplier)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(d => d.Id == documentId)
            ?? throw new InvalidOperationException($"Документ #{documentId} не найден");
    }

    public async Task<MdlpDocument> SendReceiptAsync(Document document)
    {
        var settings = await _context.MdlpSettings.FirstOrDefaultAsync() ?? new MdlpSetting
        {
            UseMock = true,
            OrgInn = "7701010000",
            OrgName = "ООО Аптека",
            SubjectId = Guid.NewGuid().ToString(),
            SimulatedDelaySeconds = 5,
            SimulatedErrorRate = 10,
            MaxRetries = 3
        };

        // Перезагружаем документ с полными связями, чтобы line.Product не был null
        var fullDocument = await LoadFullDocumentAsync(document.Id);

        var requestXml = _xmlGenerator.GenerateSchema415(fullDocument, settings.SubjectId, fullDocument.Supplier?.Inn ?? "0000000000");
        return await ProcessDocumentAsync(fullDocument, "Income", requestXml, settings, null);
    }

    public async Task<MdlpDocument> SendOutgoingAsync(Document document)
    {
        var settings = await _context.MdlpSettings.FirstOrDefaultAsync() ?? new MdlpSetting
        {
            UseMock = true,
            OrgInn = "7701010000",
            OrgName = "ООО Аптека",
            SubjectId = Guid.NewGuid().ToString(),
            SimulatedDelaySeconds = 5,
            SimulatedErrorRate = 10,
            MaxRetries = 3
        };

        var fullDocument = await LoadFullDocumentAsync(document.Id);

        var requestXml = _xmlGenerator.GenerateSchema416(fullDocument, settings.SubjectId);
        return await ProcessDocumentAsync(fullDocument, "Outcome", requestXml, settings, null);
    }

    public async Task<MdlpDocument> SendWriteOffAsync(Document document)
    {
        var settings = await _context.MdlpSettings.FirstOrDefaultAsync() ?? new MdlpSetting
        {
            UseMock = true,
            OrgInn = "7701010000",
            OrgName = "ООО Аптека",
            SubjectId = Guid.NewGuid().ToString(),
            SimulatedDelaySeconds = 5,
            SimulatedErrorRate = 10,
            MaxRetries = 3
        };

        var fullDocument = await LoadFullDocumentAsync(document.Id);

        var requestXml = _xmlGenerator.GenerateSchema552(fullDocument, settings.SubjectId);
        return await ProcessDocumentAsync(fullDocument, "WriteOff", requestXml, settings, null);
    }

    public async Task<MdlpDocument> RetryDocumentAsync(MdlpDocument previousDocument)
    {
        var settings = await _context.MdlpSettings.FirstOrDefaultAsync() ?? new MdlpSetting
        {
            UseMock = true,
            OrgInn = "7701010000",
            OrgName = "ООО Аптека",
            SubjectId = Guid.NewGuid().ToString(),
            SimulatedDelaySeconds = 5,
            SimulatedErrorRate = 10,
            MaxRetries = 3
        };

        var document = await LoadFullDocumentAsync(previousDocument.DocumentId);
        return await ProcessDocumentAsync(document, previousDocument.OperationType, previousDocument.RequestXml, settings, previousDocument);
    }

    private async Task<MdlpDocument> ProcessDocumentAsync(Document document, string operationType, string requestXml, MdlpSetting settings, MdlpDocument? previousDocument)
    {
        int retryCount = previousDocument?.RetryCount + 1 ?? 0;

        // 1. Create the document in Pending status
        var mdlpDocument = new MdlpDocument
        {
            DocumentId = document.Id,
            PreviousMdlpDocumentId = previousDocument?.Id,
            MdlpDocumentId = Guid.NewGuid().ToString(),
            OperationType = operationType,
            Status = "Pending",
            RequestXml = requestXml,
            Ticket = Guid.NewGuid().ToString(),
            SchemaVersion = "2.0",
            RetryCount = retryCount,
            CreatedAt = DateTime.Now
        };

        _context.MdlpDocuments.Add(mdlpDocument);
        await _context.SaveChangesAsync();

        // Add history entry for Pending
        var historyPending = new MdlpDocumentHistory
        {
            MdlpDocumentId = mdlpDocument.Id,
            Status = "Pending",
            ChangedAt = DateTime.Now,
            Comment = "Ожидает отправки"
        };
        _context.MdlpDocumentHistories.Add(historyPending);
        await _context.SaveChangesAsync();

        // 2. Mark as Sent
        mdlpDocument.Status = "Sent";
        mdlpDocument.SentAt = DateTime.Now;

        var historySent = new MdlpDocumentHistory
        {
            MdlpDocumentId = mdlpDocument.Id,
            Status = "Sent",
            ChangedAt = DateTime.Now,
            Comment = "Документ отправлен в МДЛП"
        };
        _context.MdlpDocumentHistories.Add(historySent);
        await _context.SaveChangesAsync();

        // 3. Determine result (success or error)
        var (detErrorCode, detErrorDesc) = await _errorGenerator.CheckDeterministicErrorsAsync(document);
        var (randErrorCode, randErrorDesc) = _errorGenerator.CheckRandomErrors(settings.SimulatedErrorRate);
        var errorCode = detErrorCode ?? randErrorCode;
        var errorDesc = detErrorDesc ?? randErrorDesc;

        string finalStatus;
        string operationId = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(errorCode))
        {
            finalStatus = "Accepted";
            mdlpDocument.Status = finalStatus;
            mdlpDocument.ProcessedAt = DateTime.Now;
            mdlpDocument.ResponseXml = _xmlGenerator.GenerateResponseXml(mdlpDocument.Ticket, operationId, finalStatus, DateTime.Now, null, null);

            // Обновляем статусы SGTIN только после успешного подтверждения МДЛП
            await UpdateSgtinStatusesAsync(document, operationType);
        }
        else
        {
            finalStatus = retryCount >= settings.MaxRetries ? "Error" : "Rejected";
            mdlpDocument.Status = finalStatus;
            mdlpDocument.ProcessedAt = DateTime.Now;
            mdlpDocument.ErrorCode = errorCode;
            mdlpDocument.ErrorMessage = errorDesc;
            mdlpDocument.ErrorDetails = errorDesc;
            mdlpDocument.ResponseXml = _xmlGenerator.GenerateResponseXml(mdlpDocument.Ticket, operationId, finalStatus, DateTime.Now, errorCode, errorDesc);
        }

        var historyFinal = new MdlpDocumentHistory
        {
            MdlpDocumentId = mdlpDocument.Id,
            Status = finalStatus,
            ChangedAt = DateTime.Now,
            Comment = finalStatus == "Accepted" ? "Документ принят МДЛП" : $"Ошибка: {errorDesc}"
        };
        _context.MdlpDocumentHistories.Add(historyFinal);
        await _context.SaveChangesAsync();

        return mdlpDocument;
    }

    private async System.Threading.Tasks.Task UpdateSgtinStatusesAsync(Document document, string operationType)
    {
        // Строки уже загружены в LoadFullDocumentAsync, но на всякий случай проверяем
        if (!document.DocumentLines.Any())
        {
            await _context.Entry(document)
                .Collection(d => d.DocumentLines)
                .LoadAsync();
        }

        foreach (var line in document.DocumentLines)
        {
            if (string.IsNullOrEmpty(line.Sgtin))
                continue;

            var sgtin = await _context.MdlpSgtins.FirstOrDefaultAsync(s => s.Sgtin == line.Sgtin);
            if (sgtin == null)
                continue;

            sgtin.PreviousStatus = sgtin.Status;
            sgtin.StatusChangedAt = DateTime.Now;

            switch (operationType)
            {
                case "Income":
                    sgtin.Status = "InCirculation";
                    break;
                case "Outcome":
                    sgtin.Status = "RetailSold";
                    break;
                case "WriteOff":
                    sgtin.Status = "WrittenOff";
                    break;
            }
        }

        await _context.SaveChangesAsync();
    }
}
