using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using System.Collections.ObjectModel;

namespace PharmacyWarehouse.Services;


public class DocumentService
{
    private readonly PharmacyWarehouseContext _db = BaseDbService.Instance.Context;
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

    public int Commit() => _db.SaveChanges();

    public int CreateOutgoing(Document document, List<DocumentLine> lines)
    {
        using var transaction = _db.Database.BeginTransaction();

        try
        {
            var amount = document.Amount;

            // Проверяем, новый ли это документ или редактирование существующего
            if (document.Id == 0) // Новый документ
            {
                document.Number = GenerateDocumentNumber(DocumentType.Outgoing);
                document.Type = DocumentType.Outgoing;
                document.CreatedAt = DateTime.Now;

                if (document.Date == default(DateTime))
                    document.Date = DateTime.Now;

                _db.Documents.Add(document);
            }
            else // Редактирование существующего документа
            {
                // Загружаем существующий документ
                var existingDocument = _db.Documents
                    .Include(d => d.DocumentLines)
                    .FirstOrDefault(d => d.Id == document.Id);

                if (existingDocument == null)
                    throw new Exception("Документ не найден");

                // Обновляем свойства документа
                existingDocument.Date = document.Date;
                existingDocument.CustomerName = document.CustomerName;
                existingDocument.CustomerDocument = document.CustomerDocument;
                existingDocument.Notes = document.Notes;
                existingDocument.Status = document.Status;
                existingDocument.Amount = document.Amount;
                existingDocument.SignedBy = document.SignedBy;
                existingDocument.SignedAt = document.SignedAt;

                // Удаляем старые строки документа
                _db.DocumentLines.RemoveRange(existingDocument.DocumentLines);

                document = existingDocument;
            }

            _db.SaveChanges();

            // Если документ проводится (не черновик), списываем товары из партий
            if (document.Status == DocumentStatus.Processed)
            {
                foreach (var line in lines)
                {
                    var batches = _db.Batches
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

                        // СОЗДАЕМ СТРОКУ ДОКУМЕНТА с данными из партии
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

                        _db.DocumentLines.Add(batchLine);

                        // Уменьшаем количество в партии
                        batch.Quantity -= toTake;
                        if (batch.Quantity == 0)
                            batch.IsActive = false;

                        remaining -= toTake;
                    }
                }

                document.SignedAt = DateTime.Now;
                if (string.IsNullOrEmpty(document.SignedBy))
                    document.SignedBy = _currentUser;   
            }
            else // Черновик - создаем строки без списания из партий
            {
                foreach (var line in lines)
                {
                    // Для черновика series должно быть заполнено из входных данных
                    // или из первой доступной партии
                    string series;

                    if (!string.IsNullOrEmpty(line.Series))
                    {
                        // Если серия указана в строке, используем ее
                        series = line.Series;
                    }
                    else
                    {
                        // Иначе берем серию из первой доступной партии
                        var batch = _db.Batches
                            .Where(b => b.ProductId == line.ProductId &&
                                       b.IsActive &&
                                       b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .FirstOrDefault();

                        series = batch?.Series ?? "БЕЗ СЕРИИ"; // Значение по умолчанию
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

                    _db.DocumentLines.Add(documentLine);
                }
            }

            document.Amount = amount;

            _db.SaveChanges();
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
        return _db.Documents
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.Product)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.CreatedBatch)
            .Include(d => d.DocumentLines)
                .ThenInclude(l => l.SourceBatch)
            .Include(d => d.Supplier)
            .Include(d => d.OriginalDocument)
            .Include(d => d.Corrections)
            .FirstOrDefault(d => d.Id == documentId);
    }

    public int CreateWriteOff(Document document, List<DocumentLine> lines)
    {
        using var transaction = _db.Database.BeginTransaction();

        try
        {
            // Инициализируем сумму документа
            decimal totalAmount = 0;

            // Проверяем, новый ли это документ или редактирование существующего
            if (document.Id == 0) // Новый документ
            {
                // Генерируем номер для списания
                document.Number = GenerateDocumentNumber(DocumentType.WriteOff);
                document.Type = DocumentType.WriteOff;
                document.CreatedAt = DateTime.Now;

                if (document.Date == default(DateTime))
                    document.Date = DateTime.Now;

                _db.Documents.Add(document);
            }
            else // Редактирование существующего документа
            {
                // Загружаем существующий документ
                var existingDocument = _db.Documents
                    .Include(d => d.DocumentLines)
                    .FirstOrDefault(d => d.Id == document.Id);

                if (existingDocument == null)
                    throw new Exception("Документ не найден");

                // Обновляем свойства документа
                existingDocument.Date = document.Date;
                existingDocument.CustomerName = document.CustomerName;
                existingDocument.CustomerDocument = document.CustomerDocument;
                existingDocument.Notes = document.Notes;
                existingDocument.Status = document.Status;
                existingDocument.SignedBy = document.SignedBy;
                existingDocument.SignedAt = document.SignedAt;
                existingDocument.Type = DocumentType.WriteOff;
                existingDocument.WriteOffReason = document.WriteOffReason;

                // Удаляем старые строки документа
                _db.DocumentLines.RemoveRange(existingDocument.DocumentLines);

                document = existingDocument;
            }

            _db.SaveChanges();

            // Если документ проводится (не черновик), списываем товары
            if (document.Status == DocumentStatus.Processed)
            {
                foreach (var line in lines)
                {
                    var batches = _db.Batches
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

                        // Рассчитываем сумму для этой строки
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

                        _db.DocumentLines.Add(batchLine);

                        batch.Quantity -= toTake;
                        if (batch.Quantity == 0)
                            batch.IsActive = false;

                        remaining -= toTake;
                    }
                }

                document.SignedAt = DateTime.Now;
                if (string.IsNullOrEmpty(document.SignedBy))
                    document.SignedBy = _currentUser;
            }
            else // Для черновика просто добавляем строки без списания
            {
                foreach (var line in lines)
                {
                    // Ищем исходные данные из партий для заполнения обязательных полей
                    var batch = _db.Batches
                        .Where(b => b.ProductId == line.ProductId &&
                                   b.IsActive &&
                                   b.Quantity > 0 &&
                                   b.Id == line.SourceBatchId)
                        .FirstOrDefault();

                    // Если не нашли по SourceBatchId, ищем первую доступную партию
                    if (batch == null && line.SourceBatchId == null)
                    {
                        batch = _db.Batches
                            .Where(b => b.ProductId == line.ProductId &&
                                       b.IsActive &&
                                       b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .FirstOrDefault();
                    }

                    // Рассчитываем сумму для строки
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

                    _db.DocumentLines.Add(documentLine);
                }
            }

            // Устанавливаем сумму документа
            document.Amount = totalAmount;

            _db.SaveChanges();
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
        using var transaction = _db.Database.BeginTransaction();

        try
        {
            var originalDoc = _db.Documents
                .Include(d => d.DocumentLines)
                    .ThenInclude(l => l.CreatedBatch)
                .FirstOrDefault(d => d.Id == originalDocumentId);

            if (originalDoc == null)
                throw new Exception("Оригинальный документ не найден");

            var correctionDoc = new Document
            {
                Type = DocumentType.Correction,
                Number = GenerateDocumentNumber(DocumentType.Correction),
                Date = DateTime.Now,
                OriginalDocumentId = originalDocumentId,
                CorrectionType = Models.CorrectionType.Quantity,
                CorrectionReason = reason,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser,
                Status = DocumentStatus.Draft
            };

            _db.Documents.Add(correctionDoc);
            _db.SaveChanges();

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

                // Обновляем количество в партии
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

                _db.DocumentLines.Add(correctionLine);

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

                _db.BatchCorrectionLogs.Add(log);
            }

            correctionDoc.Status = DocumentStatus.Processed;
            correctionDoc.SignedAt = DateTime.Now;
            correctionDoc.SignedBy = _currentUser;
            correctionDoc.Amount = totalAmount;

            _db.SaveChanges();
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
        var document = _db.Documents
            .AsNoTracking() // Важно: отключаем отслеживание
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

        // Явно загружаем корректировки отдельным запросом
        _db.Entry(document).Collection(d => d.Corrections).Query()
            .Include(c => c.DocumentLines)
                .ThenInclude(l => l.Product)
            .Load();

        return document;
    }

    public List<Document> GetDocumentCorrections(int documentId)
    {
        return _db.Documents
            .Where(d => d.OriginalDocumentId == documentId &&
                       d.Type == DocumentType.Correction)
            .Include(d => d.DocumentLines)
            .OrderByDescending(d => d.Date)
            .ToList();
    }

    public string GenerateDocumentNumber(DocumentType type)
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
        int count = _db.Documents
            .Count(d => d.Type == type &&
                       d.Date != default(DateTime)  &&
                       d.Date.Year == today.Year &&
                       d.Date.Month == today.Month) + 1;

        return $"{prefix}-{today:yyyyMM}-{count:D3}";
    }

    public void GetAll()
    {
        var documents = _db.Documents
            .Include(d => d.Supplier)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.Product)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.CreatedBatch)
            .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.SourceBatch)
            .Include(d => d.OriginalDocument)
            .Include(d => d.Corrections)
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
        return _db.Documents
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
        using var transaction = _db.Database.BeginTransaction();

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

                _db.Documents.Add(document);
            }
            else 
            {
                var existingDocument = _db.Documents
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

                _db.DocumentLines.RemoveRange(existingDocument.DocumentLines);

                document = existingDocument;
            }

            _db.SaveChanges();

            decimal totalAmount = 0;

            foreach (var line in documentLines)
            {
                line.DocumentId = document.Id;

                if (document.Status == DocumentStatus.Processed)
                {
                    var batch = new Batch
                    {
                        ProductId = line.ProductId,
                        SupplierId = document.SupplierId ?? 0,
                        Series = line.Series ?? "БЕЗ СЕРИИ",
                        ExpirationDate = line.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(1)),
                        PurchasePrice = line.UnitPrice,
                        SellingPrice = line.SellingPrice ?? line.UnitPrice * 1.3m,
                        Quantity = line.Quantity,
                        ArrivalDate = DateOnly.FromDateTime(document.Date),
                        IncomingDocumentId = document.Id,
                        IsActive = true
                    };

                    _db.Batches.Add(batch);
                    _db.SaveChanges();

                    line.CreatedBatchId = batch.Id;
                }

                _db.DocumentLines.Add(line);
                totalAmount += line.UnitPrice * line.Quantity;
            }

            document.Amount = totalAmount;
            _db.SaveChanges();

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
        using var transaction = _db.Database.BeginTransaction();

        try
        {
            var originalDoc = _db.Documents
                .Include(d => d.DocumentLines)
                    .ThenInclude(l => l.CreatedBatch)
                .FirstOrDefault(d => d.Id == originalDocumentId);

            if (originalDoc == null)
                throw new Exception("Оригинальный документ не найден");

            var correctionDoc = new Document
            {
                Type = DocumentType.Correction,
                Number = GenerateDocumentNumber(DocumentType.Correction),
                Date = DateTime.Now,
                OriginalDocumentId = originalDocumentId,
                CorrectionType = Models.CorrectionType.Price, // <-- Важно!
                CorrectionReason = reason,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUser,
                Status = DocumentStatus.Draft
            };

            _db.Documents.Add(correctionDoc);
            _db.SaveChanges();

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

                // Обновляем цену в партии
                batch.SellingPrice = newPrice;

                var correctionLine = new DocumentLine
                {
                    DocumentId = correctionDoc.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity, // Используем оригинальное количество
                    UnitPrice = line.UnitPrice,
                    SellingPrice = newPrice,
                    Series = batch.Series,
                    ExpirationDate = batch.ExpirationDate,
                    OldValue = oldPrice.ToString("N2"),
                    NewValue = newPrice.ToString("N2"),
                    CorrectionNotes = $"Корректировка цены: {oldPrice:N2} → {newPrice:N2}",
                    CreatedBatchId = batch.Id
                };

                // Считаем разницу в сумме
                decimal oldTotal = line.Quantity * oldPrice;
                decimal newTotal = line.Quantity * newPrice;
                totalAmount += newTotal - oldTotal;

                _db.DocumentLines.Add(correctionLine);

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

                _db.BatchCorrectionLogs.Add(log);
            }

            correctionDoc.Status = DocumentStatus.Processed;
            correctionDoc.SignedAt = DateTime.Now;
            correctionDoc.SignedBy = _currentUser;
            correctionDoc.Amount = totalAmount;

            _db.SaveChanges();
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
        using var transaction = _db.Database.BeginTransaction();

        try
        {
            // Находим существующий документ с загрузкой связанных данных
            var existingDocument = _db.Documents
                .Include(d => d.DocumentLines)
                    .ThenInclude(dl => dl.CreatedBatch) // Правильный синтаксис для ThenInclude
                .FirstOrDefault(d => d.Id == document.Id);

            if (existingDocument == null)
                return false;

            // Обновляем свойства документа
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

            // Удаляем старые строки и связанные партии (если есть)
            foreach (var oldLine in existingDocument.DocumentLines.ToList())
            {
                // Если была создана партия - удаляем её
                if (oldLine.CreatedBatchId.HasValue)
                {
                    var batch = _db.Batches.FirstOrDefault(b => b.Id == oldLine.CreatedBatchId.Value);
                    if (batch != null)
                    {
                        _db.Batches.Remove(batch);
                    }
                }
                _db.DocumentLines.Remove(oldLine);
            }

            _db.SaveChanges();

            // Добавляем новые строки
            decimal totalAmount = 0;
            foreach (var line in documentLines)
            {
                line.DocumentId = existingDocument.Id;
                line.Id = 0; // Сбрасываем ID, чтобы создать новую запись

                // Если документ проводится - создаем партию
                if (existingDocument.Status == DocumentStatus.Processed)
                {
                    var batch = new Batch
                    {
                        ProductId = line.ProductId,
                        SupplierId = existingDocument.SupplierId ?? 0,
                        Series = line.Series ?? "БЕЗ СЕРИИ",
                        ExpirationDate = line.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(1)),
                        PurchasePrice = line.UnitPrice,
                        SellingPrice = line.SellingPrice ?? line.UnitPrice * 1.3m,
                        Quantity = line.Quantity,
                        ArrivalDate = DateOnly.FromDateTime(existingDocument.Date),
                        IncomingDocumentId = existingDocument.Id,
                        IsActive = true
                    };

                    _db.Batches.Add(batch);
                    _db.SaveChanges();

                    line.CreatedBatchId = batch.Id;
                }

                _db.DocumentLines.Add(line);
                totalAmount += line.UnitPrice * line.Quantity;
            }

            existingDocument.Amount = totalAmount;
            _db.SaveChanges();

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

    public int GetDocumentsCount(int month, int year)
    {
        return _db.Documents
            .Count(d => d.Date.Month == month && d.Date.Year == year);
    }

    public List<Document> GetIncoming() =>
        Documents.Where(d => d.Type == DocumentType.Incoming).ToList();

    public List<Document> GetOutgoing() =>
        Documents.Where(d => d.Type == DocumentType.Outgoing).ToList();

    public List<Document> GetWriteOffs() =>
        Documents.Where(d => d.Type == DocumentType.WriteOff).ToList();
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

