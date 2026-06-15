using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using System.Collections.ObjectModel;

namespace PharmacyWarehouse.Services;


public class DocumentService
{
    public ObservableCollection<Document> Documents { get; set; } = new();
    private readonly AuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private string _currentUser = String.Empty;

    public DocumentService()
    {
        _serviceProvider = App.ServiceProvider;
        _authService = _serviceProvider.GetService<AuthService>();
        _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";

        GetAll();
    }

    public int CreateOutgoing(Document document, List<DocumentLine> lines)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            var amount = document.Amount;

            if (document.Id == 0)
            {
                document.Number = GenerateDocumentNumber(DocumentType.Outgoing, db);
                document.Type = DocumentType.Outgoing;
                document.CreatedAt = DateTime.Now;

                if (document.Date == default(DateTime))
                    document.Date = DateTime.Now;

                db.Documents.Add(document);
            }
            else
            {
                var existingDocument = db.Documents
                    .Include(d => d.DocumentLines)
                    .FirstOrDefault(d => d.Id == document.Id);

                if (existingDocument == null)
                    throw new Exception("Документ не найден");

                existingDocument.Date = document.Date;
                existingDocument.CustomerName = document.CustomerName;
                existingDocument.CustomerDocument = document.CustomerDocument;
                existingDocument.Notes = document.Notes;
                existingDocument.Status = document.Status;
                existingDocument.Amount = document.Amount;
                existingDocument.SignedBy = document.SignedBy;
                existingDocument.SignedAt = document.SignedAt;

                db.DocumentLines.RemoveRange(existingDocument.DocumentLines);

                document = existingDocument;
            }

            db.SaveChanges();

            if (document.Status == DocumentStatus.Processed)
            {
                foreach (var line in lines)
                {
                    var product = db.Products.FirstOrDefault(p => p.Id == line.ProductId);
                    if (product != null && product.IsTracked)
                    {
                        if (string.IsNullOrEmpty(line.Sgtin))
                            throw new InvalidOperationException($"Для товара '{product.Name}' необходимо указать код маркировки (SGTIN)");

                        var sgtin = db.MdlpSgtins.FirstOrDefault(s => s.Sgtin == line.Sgtin);
                        if (sgtin == null)
                            throw new InvalidOperationException($"Код маркировки '{line.Sgtin}' не найден");

                        if (sgtin.Status != "InCirculation")
                            throw new InvalidOperationException($"Код маркировки '{line.Sgtin}' имеет неверный статус: {sgtin.Status}");

                        if (sgtin.BatchId == null)
                            throw new InvalidOperationException($"Код маркировки '{line.Sgtin}' не привязан к партии");

                        var batch = db.Batches.FirstOrDefault(b => b.Id == sgtin.BatchId);
                        if (batch == null)
                            throw new InvalidOperationException($"Партия для кода маркировки '{line.Sgtin}' не найдена");

                        if (batch.Quantity < 1)
                            throw new InvalidOperationException($"Недостаточно товара в партии");

                        var batchLine = new DocumentLine
                        {
                            DocumentId = document.Id,
                            ProductId = line.ProductId,
                            Quantity = 1,
                            UnitPrice = batch.SellingPrice,
                            SellingPrice = batch.SellingPrice,
                            Series = batch.Series,
                            ExpirationDate = batch.ExpirationDate,
                            SourceBatchId = batch.Id,
                            Notes = line.Notes,
                            Sgtin = line.Sgtin
                        };

                        db.DocumentLines.Add(batchLine);

                        // Уменьшаем остаток в партии
                        batch.Quantity -= 1;
                        if (batch.Quantity == 0)
                            batch.IsActive = false;

                        // SGTIN статус НЕ меняем здесь — это сделает MdlpMockService
                        // после успешного подтверждения из МДЛП (UpdateSgtinStatusesAsync)
                    }
                    else
                    {
                        var batches = db.Batches
                            .Where(b => b.ProductId == line.ProductId &&
                                       b.IsActive &&
                                       b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .ToList();

                        if (batches.Sum(b => b.Quantity) < line.Quantity)
                            throw new InvalidOperationException($"Недостаточно товара. Нужно: {line.Quantity}, есть: {batches.Sum(b => b.Quantity)}");

                        int remaining = line.Quantity;

                        foreach (var batch in batches)
                        {
                            if (remaining <= 0) break;

                            int toTake = Math.Min(batch.Quantity, remaining);

                            var batchLine = new DocumentLine
                            {
                                DocumentId = document.Id,
                                ProductId = line.ProductId,
                                Quantity = toTake,
                                UnitPrice = batch.SellingPrice,
                                SellingPrice = batch.SellingPrice,
                                Series = batch.Series,
                                ExpirationDate = batch.ExpirationDate,
                                SourceBatchId = batch.Id,
                                Notes = line.Notes
                            };

                            db.DocumentLines.Add(batchLine);

                            batch.Quantity -= toTake;
                            if (batch.Quantity == 0)
                                batch.IsActive = false;

                            remaining -= toTake;
                        }
                    }
                }

                document.SignedAt = DateTime.Now;
                if (string.IsNullOrEmpty(document.SignedBy))
                    document.SignedBy = _currentUser;
            }
            else
            {
                foreach (var line in lines)
                {
                    string series;
                    if (!string.IsNullOrEmpty(line.Series))
                    {
                        series = line.Series;
                    }
                    else
                    {
                        var batch = db.Batches
                            .Where(b => b.ProductId == line.ProductId && b.IsActive && b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .FirstOrDefault();
                        series = batch?.Series ?? "БЕЗ СЕРИИ";
                    }

                    var documentLine = new DocumentLine
                    {
                        DocumentId = document.Id,
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        SellingPrice = line.SellingPrice,
                        Series = series,
                        ExpirationDate = line.ExpirationDate,
                        Notes = line.Notes,
                        CreatedBatchId = null,
                        SourceBatchId = null
                    };

                    db.DocumentLines.Add(documentLine);
                }
            }

            document.Amount = amount;

            db.SaveChanges();
            transaction.Commit();

            GetAll();

            return document.Id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Ошибка при создании расходной накладной: {ex.Message}");
        }
    }

    public Document GetDocumentWithDetails(int documentId)
    {
        using var db = new PharmacyWarehouseContext();
        return db.Documents
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.Product)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.CreatedBatch)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.SourceBatch)
            .Include(d => d.Supplier)
            .Include(d => d.OriginalDocument)
            .Include(d => d.InverseOriginalDocument)
            .FirstOrDefault(d => d.Id == documentId);
    }

    public int CreateWriteOff(Document document, List<DocumentLine> lines)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            decimal totalAmount = 0;

            if (document.Id == 0)
            {
                document.Number = GenerateDocumentNumber(DocumentType.WriteOff, db);
                document.Type = DocumentType.WriteOff;
                document.CreatedAt = DateTime.Now;

                if (document.Date == default(DateTime))
                    document.Date = DateTime.Now;

                db.Documents.Add(document);
            }
            else
            {
                var existingDocument = db.Documents
                    .Include(d => d.DocumentLines)
                    .FirstOrDefault(d => d.Id == document.Id);

                if (existingDocument == null)
                    throw new Exception("Документ не найден");

                existingDocument.Date = document.Date;
                existingDocument.CustomerName = document.CustomerName;
                existingDocument.CustomerDocument = document.CustomerDocument;
                existingDocument.Notes = document.Notes;
                existingDocument.Status = document.Status;
                existingDocument.SignedBy = document.SignedBy;
                existingDocument.SignedAt = document.SignedAt;
                existingDocument.Type = DocumentType.WriteOff;
                existingDocument.WriteOffReason = document.WriteOffReason;

                db.DocumentLines.RemoveRange(existingDocument.DocumentLines);

                document = existingDocument;
            }

            db.SaveChanges();

            if (document.Status == DocumentStatus.Processed)
            {
                foreach (var line in lines)
                {
                    var product = db.Products.FirstOrDefault(p => p.Id == line.ProductId);
                    if (product != null && product.IsTracked)
                    {
                        if (string.IsNullOrEmpty(line.Sgtin))
                            throw new InvalidOperationException($"Для товара '{product.Name}' необходимо указать код маркировки (SGTIN)");

                        var sgtin = db.MdlpSgtins.FirstOrDefault(s => s.Sgtin == line.Sgtin);
                        if (sgtin == null)
                            throw new InvalidOperationException($"Код маркировки '{line.Sgtin}' не найден");

                        if (sgtin.Status != "InCirculation")
                            throw new InvalidOperationException($"Код маркировки '{line.Sgtin}' имеет неверный статус: {sgtin.Status}");

                        if (sgtin.BatchId == null)
                            throw new InvalidOperationException($"Код маркировки '{line.Sgtin}' не привязан к партии");

                        var batch = db.Batches.FirstOrDefault(b => b.Id == sgtin.BatchId);
                        if (batch == null)
                            throw new InvalidOperationException($"Партия для кода маркировки '{line.Sgtin}' не найдена");

                        if (batch.Quantity < 1)
                            throw new InvalidOperationException($"Недостаточно товара в партии");

                        var batchLine = new DocumentLine
                        {
                            DocumentId = document.Id,
                            ProductId = line.ProductId,
                            Quantity = 1,
                            UnitPrice = batch.SellingPrice,
                            SellingPrice = batch.SellingPrice,
                            Series = batch.Series,
                            ExpirationDate = batch.ExpirationDate,
                            SourceBatchId = batch.Id,
                            Notes = line.Notes,
                            Sgtin = line.Sgtin
                        };

                        db.DocumentLines.Add(batchLine);

                        // Уменьшаем остаток в партии
                        batch.Quantity -= 1;
                        if (batch.Quantity == 0)
                            batch.IsActive = false;

                        totalAmount += batch.SellingPrice * 1;

                        // SGTIN статус НЕ меняем здесь — это сделает MdlpMockService
                        // после успешного подтверждения из МДЛП (UpdateSgtinStatusesAsync)
                    }
                    else
                    {
                        var batches = db.Batches
                            .Where(b => b.ProductId == line.ProductId &&
                                       b.IsActive &&
                                       b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .ToList();

                        if (batches.Sum(b => b.Quantity) < line.Quantity)
                            throw new InvalidOperationException($"Недостаточно товара. Нужно: {line.Quantity}, есть: {batches.Sum(b => b.Quantity)}");

                        int remaining = line.Quantity;

                        foreach (var batch in batches)
                        {
                            if (remaining <= 0) break;

                            int toTake = Math.Min(batch.Quantity, remaining);

                            decimal lineAmount = batch.SellingPrice * toTake;
                            totalAmount += lineAmount;

                            var batchLine = new DocumentLine
                            {
                                DocumentId = document.Id,
                                ProductId = line.ProductId,
                                Quantity = toTake,
                                UnitPrice = batch.SellingPrice,
                                SellingPrice = batch.SellingPrice,
                                Series = batch.Series,
                                ExpirationDate = batch.ExpirationDate,
                                SourceBatchId = batch.Id,
                                Notes = line.Notes
                            };

                            db.DocumentLines.Add(batchLine);

                            batch.Quantity -= toTake;
                            if (batch.Quantity == 0)
                                batch.IsActive = false;

                            remaining -= toTake;
                        }
                    }
                }

                document.SignedAt = DateTime.Now;
                if (string.IsNullOrEmpty(document.SignedBy))
                    document.SignedBy = _currentUser;
            }
            else
            {
                foreach (var line in lines)
                {
                    var batch = db.Batches
                        .Where(b => b.ProductId == line.ProductId &&
                                   b.IsActive &&
                                   b.Quantity > 0 &&
                                   b.Id == line.SourceBatchId)
                        .FirstOrDefault();

                    if (batch == null && line.SourceBatchId == null)
                    {
                        batch = db.Batches
                            .Where(b => b.ProductId == line.ProductId && b.IsActive && b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .FirstOrDefault();
                    }

                    decimal sellingPrice = line.SellingPrice ?? batch?.SellingPrice ?? 0m;
                    decimal lineAmount = sellingPrice * line.Quantity;
                    totalAmount += lineAmount;

                    var documentLine = new DocumentLine
                    {
                        DocumentId = document.Id,
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice > 0 ? line.UnitPrice : (batch?.SellingPrice ?? 0m),
                        SellingPrice = sellingPrice,
                        Series = line.Series ?? batch?.Series ?? "БЕЗ СЕРИИ",
                        ExpirationDate = line.ExpirationDate ?? batch?.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now),
                        Notes = line.Notes,
                        CreatedBatchId = null,
                        SourceBatchId = line.SourceBatchId ?? batch?.Id
                    };

                    db.DocumentLines.Add(documentLine);
                }
            }

            document.Amount = totalAmount;

            db.SaveChanges();
            transaction.Commit();

            GetAll();

            return document.Id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Ошибка при создании акта списания: {ex.Message}", ex);
        }
    }

    public int CreateQuantityCorrection(int originalDocumentId, string reason,
                                        List<QuantityCorrection> corrections)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            var originalDoc = db.Documents
                .Include(d => d.DocumentLines)
                    .ThenInclude(l => l.CreatedBatch)
                .FirstOrDefault(d => d.Id == originalDocumentId);

            if (originalDoc == null)
                throw new Exception("Оригинальный документ не найден");

            var correctionDoc = new Document
            {
                Type = DocumentType.Correction,
                Number = GenerateDocumentNumber(DocumentType.Correction, db),
                Date = DateTime.Now,
                OriginalDocumentId = originalDocumentId,
                CorrectionType = Models.CorrectionType.Quantity,
                CorrectionReason = reason,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser,
                Status = DocumentStatus.Draft
            };

            db.Documents.Add(correctionDoc);
            db.SaveChanges();

            decimal totalAmount = 0;

            foreach (var correction in corrections)
            {
                var line = originalDoc.DocumentLines
                    .FirstOrDefault(l => l.Id == correction.DocumentLineId);

                if (line == null || line.CreatedBatch == null)
                    continue;

                var batch = line.CreatedBatch;
                var oldQuantity = batch.Quantity;
                var newQuantity = correction.NewQuantity;

                if (newQuantity < 0)
                    throw new Exception($"Количество не может быть отрицательным: {newQuantity}");

                batch.Quantity = newQuantity;

                if (batch.Quantity == 0)
                    batch.IsActive = false;

                var correctionLine = new DocumentLine
                {
                    DocumentId = correctionDoc.Id,
                    ProductId = line.ProductId,
                    Quantity = Math.Abs(newQuantity - oldQuantity),
                    UnitPrice = line.UnitPrice,
                    Series = batch.Series,
                    ExpirationDate = batch.ExpirationDate,
                    OldValue = oldQuantity.ToString(),
                    NewValue = newQuantity.ToString(),
                    CorrectionNotes = $"Корректировка количества: {oldQuantity} → {newQuantity}",
                    CreatedBatchId = batch.Id
                };

                decimal oldTotal = oldQuantity * line.UnitPrice;
                decimal newTotal = newQuantity * line.UnitPrice;
                totalAmount += newTotal - oldTotal;

                db.DocumentLines.Add(correctionLine);

                var log = new BatchCorrectionLog
                {
                    BatchId = batch.Id,
                    CorrectionDocumentId = correctionDoc.Id,
                    CorrectionDate = DateTime.Now,
                    FieldName = "Quantity",
                    OldValue = oldQuantity.ToString(),
                    NewValue = newQuantity.ToString(),
                    ChangedBy = _currentUser,
                    Reason = reason
                };

                db.BatchCorrectionLogs.Add(log);
            }

            correctionDoc.Status = DocumentStatus.Processed;
            correctionDoc.SignedAt = DateTime.Now;
            correctionDoc.SignedBy = _currentUser;
            correctionDoc.Amount = totalAmount;

            db.SaveChanges();
            transaction.Commit();

            GetAll();
            return correctionDoc.Id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Ошибка при создании корректировки: {ex.Message}");
        }
    }

    public Document? GetDocumentWithCorrections(int documentId)
    {
        using var db = new PharmacyWarehouseContext();
        var document = db.Documents
            .AsNoTracking()
            .Include(d => d.Supplier)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.Product)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.CreatedBatch)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.SourceBatch)
            .FirstOrDefault(d => d.Id == documentId);

        if (document == null)
            return null;

        db.Entry(document).Collection(d => d.InverseOriginalDocument).Query()
            .Include(c => c.DocumentLines)
                .ThenInclude(l => l.Product)
            .Load();

        return document;
    }

    public List<Document> GetDocumentCorrections(int documentId)
    {
        using var db = new PharmacyWarehouseContext();
        return db.Documents
            .Where(d => d.OriginalDocumentId == documentId &&
                       d.Type == DocumentType.Correction)
            .Include(d => d.DocumentLines)
            .OrderByDescending(d => d.Date)
            .ToList();
    }

    public string GenerateDocumentNumber(DocumentType type, PharmacyWarehouseContext context)
    {
        var prefix = type switch
        {
            DocumentType.Incoming => "ПН",
            DocumentType.Outgoing => "РН",
            DocumentType.WriteOff => "АС",
            DocumentType.Correction => "КОР",
            _ => "ДОК"
        };

        var today = DateTime.Today;
        int count = context.Documents
            .Count(d => d.Type == type &&
                       d.Date != default(DateTime) &&
                       d.Date.Year == today.Year &&
                       d.Date.Month == today.Month) + 1;

        return $"{prefix}-{today:yyyyMM}-{count:D3}";
    }

    public string GenerateDocumentNumber(DocumentType type)
    {
        using var db = new PharmacyWarehouseContext();
        return GenerateDocumentNumber(type, db);
    }

    public void GetAll()
    {
        using var db = new PharmacyWarehouseContext();
        var documents = db.Documents
            .Include(d => d.Supplier)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.Product)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.CreatedBatch)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.SourceBatch)
            .Include(d => d.OriginalDocument)
            .Include(d => d.InverseOriginalDocument)
            .OrderByDescending(d => d.Date)
            .ToList();

        Documents.Clear();
        foreach (var document in documents)
        {
            Documents.Add(document);
        }
    }

    public Document? GetById(int id)
    {
        using var db = new PharmacyWarehouseContext();
        return db.Documents
            .Include(d => d.Supplier)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.Product)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.CreatedBatch)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.SourceBatch)
            .FirstOrDefault(d => d.Id == id);
    }

    public int CreateIncomingDocument(Document document, List<DocumentLine> documentLines)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            if (document.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(document.Number))
                {
                    var date = document.Date;
                    var count = GetDocumentsCount(date.Month, date.Year);
                    document.Number = $"ПР-{date:MMyy}-{count + 1:D4}";
                }

                db.Documents.Add(document);
            }
            else
            {
                var existingDocument = db.Documents
                    .Include(d => d.DocumentLines)
                    .FirstOrDefault(d => d.Id == document.Id);

                if (existingDocument == null)
                    throw new Exception("Документ не найден");

                existingDocument.Date = document.Date;
                existingDocument.SupplierId = document.SupplierId;
                existingDocument.SupplierInvoiceNumber = document.SupplierInvoiceNumber;
                existingDocument.SupplierInvoiceDate = document.SupplierInvoiceDate;
                existingDocument.Notes = document.Notes;
                existingDocument.Amount = document.Amount;
                existingDocument.Status = document.Status;
                existingDocument.SignedBy = document.SignedBy;
                existingDocument.SignedAt = document.SignedAt;

                db.DocumentLines.RemoveRange(existingDocument.DocumentLines);

                document = existingDocument;
            }

            db.SaveChanges();

            decimal totalAmount = 0;

            foreach (var line in documentLines)
            {
                var newLine = new DocumentLine
                {
                    DocumentId = document.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    SellingPrice = line.SellingPrice,
                    Series = line.Series,
                    ExpirationDate = line.ExpirationDate,
                    Notes = line.Notes,
                    Sgtin = line.Sgtin
                };

                if (document.Status == DocumentStatus.Processed)
                {
                    var batch = new Batch
                    {
                        ProductId = newLine.ProductId,
                        SupplierId = document.SupplierId ?? 0,
                        Series = newLine.Series ?? "БЕЗ СЕРИИ",
                        ExpirationDate = newLine.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(1)),
                        PurchasePrice = newLine.UnitPrice,
                        SellingPrice = newLine.SellingPrice ?? newLine.UnitPrice * 1.3m,
                        Quantity = newLine.Quantity,
                        ArrivalDate = DateOnly.FromDateTime(document.Date),
                        IncomingDocumentId = document.Id,
                        IsActive = true
                    };

                    db.Batches.Add(batch);
                    db.SaveChanges();

                    newLine.CreatedBatchId = batch.Id;

                    if (!string.IsNullOrEmpty(newLine.Sgtin))
                    {
                        var mdlpSgtin = new MdlpSgtin
                        {
                            Sgtin = newLine.Sgtin,
                            Gtin = newLine.Sgtin.Substring(0, 14),
                            SerialNumber = newLine.Sgtin.Substring(14),
                            ProductId = newLine.ProductId,
                            BatchId = batch.Id,
                            Status = "InCirculation",
                            CreatedAt = DateTime.Now
                        };
                        db.MdlpSgtins.Add(mdlpSgtin);
                        db.SaveChanges();
                    }
                }

                db.DocumentLines.Add(newLine);
                totalAmount += newLine.UnitPrice * newLine.Quantity;
            }

            document.Amount = totalAmount;
            db.SaveChanges();

            transaction.Commit();
            GetAll();
            return document.Id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new Exception($"Ошибка сохранения приходного документа: {ex.Message}", ex);
        }
    }

    public int CreatePriceCorrection(int originalDocumentId, string reason,
                                 List<PriceCorrection> corrections)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            var originalDoc = db.Documents
                .Include(d => d.DocumentLines)
                    .ThenInclude(l => l.CreatedBatch)
                .FirstOrDefault(d => d.Id == originalDocumentId);

            if (originalDoc == null)
                throw new Exception("Оригинальный документ не найден");

            var correctionDoc = new Document
            {
                Type = DocumentType.Correction,
                Number = GenerateDocumentNumber(DocumentType.Correction, db),
                Date = DateTime.Now,
                OriginalDocumentId = originalDocumentId,
                CorrectionType = Models.CorrectionType.Price,
                CorrectionReason = reason,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser,
                Status = DocumentStatus.Draft
            };

            db.Documents.Add(correctionDoc);
            db.SaveChanges();

            decimal totalAmount = 0;

            foreach (var correction in corrections)
            {
                var line = originalDoc.DocumentLines
                    .FirstOrDefault(l => l.Id == correction.DocumentLineId);

                if (line == null || line.CreatedBatch == null)
                    continue;

                var batch = line.CreatedBatch;
                var oldPrice = batch.SellingPrice;
                var newPrice = correction.NewPrice;

                if (newPrice < 0)
                    throw new Exception($"Цена не может быть отрицательной: {newPrice}");

                batch.SellingPrice = newPrice;

                var correctionLine = new DocumentLine
                {
                    DocumentId = correctionDoc.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    SellingPrice = newPrice,
                    Series = batch.Series,
                    ExpirationDate = batch.ExpirationDate,
                    OldValue = oldPrice.ToString("N2"),
                    NewValue = newPrice.ToString("N2"),
                    CorrectionNotes = $"Корректировка цены: {oldPrice:N2} → {newPrice:N2}",
                    CreatedBatchId = batch.Id
                };

                decimal oldTotal = line.Quantity * oldPrice;
                decimal newTotal = line.Quantity * newPrice;
                totalAmount += newTotal - oldTotal;

                db.DocumentLines.Add(correctionLine);

                var log = new BatchCorrectionLog
                {
                    BatchId = batch.Id,
                    CorrectionDocumentId = correctionDoc.Id,
                    CorrectionDate = DateTime.Now,
                    FieldName = "Price",
                    OldValue = oldPrice.ToString("N2"),
                    NewValue = newPrice.ToString("N2"),
                    ChangedBy = _currentUser,
                    Reason = reason
                };

                db.BatchCorrectionLogs.Add(log);
            }

            correctionDoc.Status = DocumentStatus.Processed;
            correctionDoc.SignedAt = DateTime.Now;
            correctionDoc.SignedBy = _currentUser;
            correctionDoc.Amount = totalAmount;

            db.SaveChanges();
            transaction.Commit();

            GetAll();
            return correctionDoc.Id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Ошибка при создании корректировки цены: {ex.Message}");
        }
    }

    public bool UpdateDocument(Document document, List<DocumentLine> documentLines)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            var existingDocument = db.Documents
                .Include(d => d.DocumentLines)
                    .ThenInclude(dl => dl.CreatedBatch)
                .FirstOrDefault(d => d.Id == document.Id);

            if (existingDocument == null)
                return false;

            existingDocument.Date = document.Date;
            existingDocument.SupplierId = document.SupplierId;
            existingDocument.SupplierInvoiceNumber = document.SupplierInvoiceNumber;
            existingDocument.SupplierInvoiceDate = document.SupplierInvoiceDate;
            existingDocument.Notes = document.Notes;
            existingDocument.Status = document.Status;

            if (document.SignedBy != null)
                existingDocument.SignedBy = document.SignedBy;

            if (document.SignedAt.HasValue)
                existingDocument.SignedAt = document.SignedAt;

            foreach (var oldLine in existingDocument.DocumentLines.ToList())
            {
                if (oldLine.CreatedBatchId.HasValue)
                {
                    var batch = db.Batches.FirstOrDefault(b => b.Id == oldLine.CreatedBatchId.Value);
                    if (batch != null)
                        db.Batches.Remove(batch);
                }
                db.DocumentLines.Remove(oldLine);
            }

            db.SaveChanges();

            decimal totalAmount = 0;
            foreach (var line in documentLines)
            {
                var newLine = new DocumentLine
                {
                    DocumentId = existingDocument.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    SellingPrice = line.SellingPrice,
                    Series = line.Series,
                    ExpirationDate = line.ExpirationDate,
                    Notes = line.Notes,
                    Sgtin = line.Sgtin
                };

                if (existingDocument.Status == DocumentStatus.Processed)
                {
                    var batch = new Batch
                    {
                        ProductId = newLine.ProductId,
                        SupplierId = existingDocument.SupplierId ?? 0,
                        Series = newLine.Series ?? "БЕЗ СЕРИИ",
                        ExpirationDate = newLine.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(1)),
                        PurchasePrice = newLine.UnitPrice,
                        SellingPrice = newLine.SellingPrice ?? newLine.UnitPrice * 1.3m,
                        Quantity = newLine.Quantity,
                        ArrivalDate = DateOnly.FromDateTime(existingDocument.Date),
                        IncomingDocumentId = existingDocument.Id,
                        IsActive = true
                    };

                    db.Batches.Add(batch);
                    db.SaveChanges();

                    newLine.CreatedBatchId = batch.Id;
                }

                db.DocumentLines.Add(newLine);
                totalAmount += newLine.UnitPrice * newLine.Quantity;
            }

            existingDocument.Amount = totalAmount;
            db.SaveChanges();

            transaction.Commit();
            GetAll();
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new Exception($"Ошибка обновления документа: {ex.Message}", ex);
        }
    }

    public int GetDocumentsCount(int month, int year, DocumentType? type = null, PharmacyWarehouseContext? context = null)
    {
        var db = context ?? new PharmacyWarehouseContext();
        var query = db.Documents.Where(d => d.Date.Month == month && d.Date.Year == year);
        if (type.HasValue)
            query = query.Where(d => d.Type == type.Value);
        return query.Count();
    }

    public List<Document> GetIncoming() =>
        Documents.Where(d => d.Type == DocumentType.Incoming).ToList();

    public List<Document> GetOutgoing() =>
        Documents.Where(d => d.Type == DocumentType.Outgoing).ToList();

    public List<Document> GetWriteOffs() =>
        Documents.Where(d => d.Type == DocumentType.WriteOff).ToList();

    public bool ProcessDocument(int id, string signedBy)
    {
        using var db = new PharmacyWarehouseContext();
        var document = db.Documents.FirstOrDefault(d => d.Id == id);
        if (document == null) return false;

        document.Status = DocumentStatus.Processed;
        document.SignedAt = DateTime.Now;
        document.SignedBy = signedBy;

        db.SaveChanges();
        GetAll();
        return true;
    }

    public bool BlockDocument(int id)
    {
        using var db = new PharmacyWarehouseContext();
        var document = db.Documents.FirstOrDefault(d => d.Id == id);
        if (document == null) return false;

        document.Status = DocumentStatus.Blocked;

        db.SaveChanges();
        GetAll();
        return true;
    }

    public bool DeleteDocument(int id)
    {
        using var db = new PharmacyWarehouseContext();
        var document = db.Documents
            .Include(d => d.DocumentLines)
            .FirstOrDefault(d => d.Id == id);

        if (document == null) return false;

        db.DocumentLines.RemoveRange(document.DocumentLines);
        db.Documents.Remove(document);

        db.SaveChanges();
        GetAll();
        return true;
    }
}

public class QuantityCorrection
{
    public int DocumentLineId { get; set; }
    public int NewQuantity { get; set; }
}
public class PriceCorrection
{
    public int DocumentLineId { get; set; }
    public decimal NewPrice { get; set; }
}
