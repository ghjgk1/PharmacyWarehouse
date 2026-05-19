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

        var (detErrorCode, detErrorDesc) = await _errorGenerator.CheckDeterministicErrorsAsync(document);
        var (randErrorCode, randErrorDesc) = _errorGenerator.CheckRandomErrors(settings.SimulatedErrorRate);
        var errorCode = detErrorCode ?? randErrorCode;
        var errorDesc = detErrorDesc ?? randErrorDesc;

        string ticket = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();
        string status = string.IsNullOrEmpty(errorCode) ? "Pending" : "Rejected";

        var mdlpDocument = new MdlpDocument
        {
            DocumentId = document.Id,
            MdlpDocumentId = Guid.NewGuid().ToString(),
            OperationType = "Приёмка",
            Status = status,
            RequestXml = _xmlGenerator.GenerateSchema415(document, settings.SubjectId, document.Supplier?.Inn ?? "0000000000"),
            ResponseXml = _xmlGenerator.GenerateResponseXml(ticket, operationId, status, DateTime.Now, errorCode, errorDesc),
            Ticket = ticket,
            SchemaVersion = "2.0",
            ErrorCode = errorCode,
            ErrorMessage = errorDesc,
            ErrorDetails = errorDesc,
            RetryCount = 0,
            SentAt = DateTime.Now,
            ProcessedAt = status == "Rejected" ? DateTime.Now : null,
            CreatedAt = DateTime.Now
        };

        _context.MdlpDocuments.Add(mdlpDocument);

        var history = new MdlpDocumentHistory
        {
            MdlpDocumentId = mdlpDocument.Id,
            Status = status,
            ChangedAt = DateTime.Now,
            Comment = status == "Pending" ? "Документ отправлен в обработку" : "Документ отклонён"
        };
        _context.MdlpDocumentHistories.Add(history);

        await _context.SaveChangesAsync();
        return mdlpDocument;
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

        var (detErrorCode, detErrorDesc) = await _errorGenerator.CheckDeterministicErrorsAsync(document);
        var (randErrorCode, randErrorDesc) = _errorGenerator.CheckRandomErrors(settings.SimulatedErrorRate);
        var errorCode = detErrorCode ?? randErrorCode;
        var errorDesc = detErrorDesc ?? randErrorDesc;

        string ticket = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();
        string status = string.IsNullOrEmpty(errorCode) ? "Pending" : "Rejected";

        var mdlpDocument = new MdlpDocument
        {
            DocumentId = document.Id,
            MdlpDocumentId = Guid.NewGuid().ToString(),
            OperationType = "Отгрузка",
            Status = status,
            RequestXml = _xmlGenerator.GenerateSchema416(document, settings.SubjectId),
            ResponseXml = _xmlGenerator.GenerateResponseXml(ticket, operationId, status, DateTime.Now, errorCode, errorDesc),
            Ticket = ticket,
            SchemaVersion = "2.0",
            ErrorCode = errorCode,
            ErrorMessage = errorDesc,
            ErrorDetails = errorDesc,
            RetryCount = 0,
            SentAt = DateTime.Now,
            ProcessedAt = status == "Rejected" ? DateTime.Now : null,
            CreatedAt = DateTime.Now
        };

        _context.MdlpDocuments.Add(mdlpDocument);

        var history = new MdlpDocumentHistory
        {
            MdlpDocumentId = mdlpDocument.Id,
            Status = status,
            ChangedAt = DateTime.Now,
            Comment = status == "Pending" ? "Документ отправлен в обработку" : "Документ отклонён"
        };
        _context.MdlpDocumentHistories.Add(history);

        await _context.SaveChangesAsync();
        return mdlpDocument;
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

        var (detErrorCode, detErrorDesc) = await _errorGenerator.CheckDeterministicErrorsAsync(document);
        var (randErrorCode, randErrorDesc) = _errorGenerator.CheckRandomErrors(settings.SimulatedErrorRate);
        var errorCode = detErrorCode ?? randErrorCode;
        var errorDesc = detErrorDesc ?? randErrorDesc;

        string ticket = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();
        string status = string.IsNullOrEmpty(errorCode) ? "Pending" : "Rejected";

        var mdlpDocument = new MdlpDocument
        {
            DocumentId = document.Id,
            MdlpDocumentId = Guid.NewGuid().ToString(),
            OperationType = "Списание",
            Status = status,
            RequestXml = _xmlGenerator.GenerateSchema552(document, settings.SubjectId),
            ResponseXml = _xmlGenerator.GenerateResponseXml(ticket, operationId, status, DateTime.Now, errorCode, errorDesc),
            Ticket = ticket,
            SchemaVersion = "2.0",
            ErrorCode = errorCode,
            ErrorMessage = errorDesc,
            ErrorDetails = errorDesc,
            RetryCount = 0,
            SentAt = DateTime.Now,
            ProcessedAt = status == "Rejected" ? DateTime.Now : null,
            CreatedAt = DateTime.Now
        };

        _context.MdlpDocuments.Add(mdlpDocument);

        var history = new MdlpDocumentHistory
        {
            MdlpDocumentId = mdlpDocument.Id,
            Status = status,
            ChangedAt = DateTime.Now,
            Comment = status == "Pending" ? "Документ отправлен в обработку" : "Документ отклонён"
        };
        _context.MdlpDocumentHistories.Add(history);

        await _context.SaveChangesAsync();
        return mdlpDocument;
    }
}
