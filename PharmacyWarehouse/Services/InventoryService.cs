using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services.Mdlp;
using Microsoft.Extensions.DependencyInjection;

namespace PharmacyWarehouse.Services;

public class InventoryService
{
    private readonly string _currentUser;

    public InventoryService()
    {
        _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";
    }

    public Inventory CreateInventory(DateOnly date, string createdBy)
    {
        using var db = new PharmacyWarehouseContext();
        
        var activeBatches = db.Batches
            .Where(b => b.IsActive && b.Quantity > 0)
            .Include(b => b.Product)
            .AsNoTracking()
            .ToList();

        var count = db.Inventories.Count(i => i.InventoryDate.Year == date.Year && i.InventoryDate.Month == date.Month);
        var inventory = new Inventory
        {
            Number = $"ИНВ-{date:MM.yy}-{count + 1:D4}",
            InventoryDate = date,
            Status = "Draft",
            CreatedBy = createdBy,
            CreatedAt = DateTime.Now
        };

        db.Inventories.Add(inventory);
        db.SaveChanges();

        foreach (var batch in activeBatches)
        {
            db.InventoryLines.Add(new InventoryLine
            {
                InventoryId = inventory.Id,
                BatchId = batch.Id,
                ExpectedQuantity = batch.Quantity
            });
        }

        db.SaveChanges();
        return GetWithLines(inventory.Id)!;
    }

    public void SaveActualQuantities(int inventoryId, List<InventoryLine> lines)
    {
        using var db = new PharmacyWarehouseContext();
        
        var inventory = db.Inventories
            .Include(i => i.InventoryLines)
                .ThenInclude(l => l.InventoryScannedCodes)
            .FirstOrDefault(i => i.Id == inventoryId);

        if (inventory == null)
            throw new InvalidOperationException("Акт инвентаризации не найден");

        if (inventory.Status == "Completed")
            throw new InvalidOperationException("Завершённую инвентаризацию нельзя редактировать");

        foreach (var line in lines)
        {
            var existing = inventory.InventoryLines.FirstOrDefault(l => l.Id == line.Id);
            if (existing != null)
            {
                existing.ActualQuantity = line.ActualQuantity;
                existing.Notes = line.Notes;

                // Обновляем сканированные коды
                // Удаляем старые
                db.InventoryScannedCodes.RemoveRange(existing.InventoryScannedCodes);
                // Добавляем новые
                foreach (var sgtin in line.ScannedCodes)
                {
                    existing.InventoryScannedCodes.Add(new InventoryScannedCode
                    {
                        InventoryLineId = existing.Id,
                        Sgtin = sgtin
                    });
                }
            }
        }

        db.SaveChanges();
    }

    public async System.Threading.Tasks.Task<(bool WriteOffCreated, bool ReceiptCreated, int WriteOffLines, int ReceiptLines)> CompleteInventoryAsync(int inventoryId)
    {
        string inventoryNumber;
        List<(int LineId, int BatchId, int ProductId, int? ExpectedQty, int? ActualQty, bool IsTracked, decimal PurchasePrice, string Series, DateOnly? ExpiryDate)> diffLineData;
        Dictionary<int, List<string>> scannedCodesByLineId;

        // First, load inventory data (no tracking)
        using (var db = new PharmacyWarehouseContext())
        {
            var inventory = db.Inventories
                .Include(i => i.InventoryLines)
                    .ThenInclude(l => l.InventoryScannedCodes)
                .Include(i => i.InventoryLines)
                    .ThenInclude(l => l.Batch)
                        .ThenInclude(b => b.Product)
                .FirstOrDefault(i => i.Id == inventoryId);

            if (inventory == null)
                throw new InvalidOperationException("Акт инвентаризации не найден");

            if (inventory.Status == "Completed")
                throw new InvalidOperationException("Инвентаризация уже завершена");

            inventoryNumber = inventory.Number;

            // Update inventory status
            inventory.Status = "Completed";
            inventory.CompletedAt = DateTime.Now;

            // Get diff lines data
            diffLineData = inventory.InventoryLines
                .Where(l => l.ActualQuantity.HasValue && l.Difference != 0)
                .Select(l => (
                    LineId: l.Id,                          // 1. Переименовали Id -> LineId
                    BatchId: l.BatchId,
                    ProductId: l.Batch.ProductId,
                    ExpectedQty: (int?)l.ExpectedQuantity, // 2. Привели int к int?
                    ActualQty: l.ActualQuantity,           // Уже int?, подходит
                    IsTracked: l.Batch.Product.IsTracked,
                    PurchasePrice: l.Batch.PurchasePrice,
                    Series: l.Batch.Series,
                    ExpiryDate: (System.DateOnly?)l.Batch.ExpirationDate // 3. Привели DateOnly к DateOnly?
                ))
                .ToList();

            scannedCodesByLineId = inventory.InventoryLines
                .ToDictionary(
                    l => l.Id, 
                    l => l.InventoryScannedCodes.Select(c => c.Sgtin).ToList()
                );

            // Update batch quantities
            foreach (var line in inventory.InventoryLines.Where(l => l.ActualQuantity.HasValue && l.Difference != 0))
            {
                line.Batch.Quantity = line.ActualQuantity!.Value;
                if (line.Batch.Quantity == 0)
                    line.Batch.IsActive = false;
            }

            db.SaveChanges();
        }

        // Second, create documents using completely new DbContext to avoid conflicts!
        Document? writeOffDoc = null;
        Document? receiptDoc = null;
        int writeOffLinesCount = 0;
        int receiptLinesCount = 0;
        
        using (var dbDoc = new PharmacyWarehouseContext())
        {
            // Create write-off document
            if (diffLineData.Any(d => (d.ActualQty ?? 0) - (d.ExpectedQty ?? 0) < 0))
            {
                writeOffDoc = new Document
                {
                    Type = DocumentType.WriteOff,
                    Number = GenerateDocumentNumber(DocumentType.WriteOff),
                    Date = DateTime.Now,
                    Status = DocumentStatus.Processed,
                    CreatedBy = _currentUser,
                    CreatedAt = DateTime.Now,
                    WriteOffReason = WriteOffReason.InventoryDifference,
                    Notes = $"Инвентаризация {inventoryNumber}",
                    SignedBy = _currentUser,
                    SignedAt = DateTime.Now
                };
                dbDoc.Documents.Add(writeOffDoc);
                await dbDoc.SaveChangesAsync();

                decimal writeOffTotal = 0;
                var writeOffLines = diffLineData.Where(d => (d.ActualQty ?? 0) - (d.ExpectedQty ?? 0) < 0);
                foreach (var line in writeOffLines)
                {
                    var diff = Math.Abs((line.ActualQty ?? 0) - (line.ExpectedQty ?? 0));
                    
                    if (line.IsTracked)
                    {
                        List<string> sgtinsToUse;
                        if (scannedCodesByLineId.TryGetValue(line.LineId, out var scanned) && scanned.Count >= diff)
                        {
                            sgtinsToUse = scanned.Take(diff).ToList();
                        }
                        else
                        {
                            // Fallback
                            sgtinsToUse = await dbDoc.MdlpSgtins
                                .Where(s => s.BatchId == line.BatchId && s.Status == "InCirculation")
                                .Take(diff)
                                .Select(s => s.Sgtin)
                                .ToListAsync();
                        }

                        foreach (var sgtin in sgtinsToUse)
                        {
                            var docLine = new DocumentLine
                            {
                                DocumentId = writeOffDoc.Id,
                                ProductId = line.ProductId,
                                Quantity = 1,
                                UnitPrice = line.PurchasePrice,
                                Series = line.Series,
                                ExpirationDate = line.ExpiryDate,
                                SourceBatchId = line.BatchId,
                                Sgtin = sgtin,
                                Notes = $"Инвентаризация"
                            };
                            dbDoc.DocumentLines.Add(docLine);
                            writeOffTotal += line.PurchasePrice;
                            writeOffLinesCount++;
                        }
                    }
                    else
                    {
                        var docLine = new DocumentLine
                        {
                            DocumentId = writeOffDoc.Id,
                            ProductId = line.ProductId,
                            Quantity = diff,
                            UnitPrice = line.PurchasePrice,
                            Series = line.Series,
                            ExpirationDate = line.ExpiryDate,
                            SourceBatchId = line.BatchId,
                            Notes = $"Инвентаризация"
                        };
                        dbDoc.DocumentLines.Add(docLine);
                        writeOffTotal += diff * line.PurchasePrice;
                        writeOffLinesCount++;
                    }
                }

                writeOffDoc.Amount = writeOffTotal;
                await dbDoc.SaveChangesAsync();
            }

            // Create receipt document
            if (diffLineData.Any(d => (d.ActualQty ?? 0) - (d.ExpectedQty ?? 0) > 0))
            {
                receiptDoc = new Document
                {
                    Type = DocumentType.Incoming,
                    Number = GenerateDocumentNumber(DocumentType.Incoming),
                    Date = DateTime.Now,
                    Status = DocumentStatus.Processed,
                    CreatedBy = _currentUser,
                    CreatedAt = DateTime.Now,
                    Notes = $"Инвентаризация {inventoryNumber}",
                    SignedBy = _currentUser,
                    SignedAt = DateTime.Now
                };
                dbDoc.Documents.Add(receiptDoc);
                await dbDoc.SaveChangesAsync();

                decimal receiptTotal = 0;
                var receiptLines = diffLineData.Where(d => (d.ActualQty ?? 0) - (d.ExpectedQty ?? 0) > 0);
                foreach (var line in receiptLines)
                {
                    var diff = (line.ActualQty ?? 0) - (line.ExpectedQty ?? 0);
                    var docLine = new DocumentLine
                    {
                        DocumentId = receiptDoc.Id,
                        ProductId = line.ProductId,
                        Quantity = diff,
                        UnitPrice = line.PurchasePrice,
                        SellingPrice = line.PurchasePrice * 1.3m,
                        Series = line.Series,
                        ExpirationDate = line.ExpiryDate,
                        SourceBatchId = line.BatchId,
                        Notes = $"Инвентаризация"
                    };
                    dbDoc.DocumentLines.Add(docLine);
                    receiptTotal += diff * line.PurchasePrice;
                    receiptLinesCount++;
                }

                receiptDoc.Amount = receiptTotal;
                await dbDoc.SaveChangesAsync();
            }
        }

        // Send to MDLP
        var mdlpService = App.ServiceProvider.GetService<IMdlpService>();
        if (mdlpService != null)
        {
            using (var dbFinal = new PharmacyWarehouseContext())
            {
                if (writeOffDoc != null)
                {
                    var fullWriteOffDoc = await dbFinal.Documents
                        .Include(d => d.DocumentLines)
                        .FirstOrDefaultAsync(d => d.Id == writeOffDoc.Id);
                    if (fullWriteOffDoc != null)
                        await mdlpService.SendWriteOffAsync(fullWriteOffDoc);
                }
                if (receiptDoc != null)
                {
                    var fullReceiptDoc = await dbFinal.Documents
                        .Include(d => d.DocumentLines)
                        .FirstOrDefaultAsync(d => d.Id == receiptDoc.Id);
                    if (fullReceiptDoc != null)
                        await mdlpService.SendReceiptAsync(fullReceiptDoc);
                }
            }
        }

        return (
            WriteOffCreated: writeOffDoc != null, 
            ReceiptCreated: receiptDoc != null, 
            WriteOffLines: writeOffLinesCount, 
            ReceiptLines: receiptLinesCount
        );
    }

    public List<Inventory> GetAll()
    {
        using var db = new PharmacyWarehouseContext();
        return db.Inventories
            .OrderByDescending(i => i.InventoryDate)
            .ThenByDescending(i => i.Id)
            .AsNoTracking()
            .ToList();
    }

    public Inventory? GetWithLines(int id)
    {
        using var db = new PharmacyWarehouseContext();
        var inventory = db.Inventories
            .Include(i => i.InventoryLines)
                .ThenInclude(l => l.InventoryScannedCodes)
            .Include(i => i.InventoryLines)
                .ThenInclude(l => l.Batch)
                    .ThenInclude(b => b.Product)
            .AsNoTracking()
            .FirstOrDefault(i => i.Id == id);

        if (inventory != null)
        {
            foreach (var line in inventory.InventoryLines)
            {
                // Заполняем ObservableCollection из сохранённых кодов
                foreach (var code in line.InventoryScannedCodes.OrderBy(c => c.Id).Select(c => c.Sgtin))
                {
                    line.ScannedCodes.Add(code);
                }
            }
        }

        return inventory;
    }

    public void DeleteInventory(int inventoryId)
    {
        using var db = new PharmacyWarehouseContext();
        
        var inventory = db.Inventories
            .Include(i => i.InventoryLines)
            .FirstOrDefault(i => i.Id == inventoryId);

        if (inventory == null)
            throw new InvalidOperationException("Акт инвентаризации не найден");

        if (inventory.Status != "Draft")
            throw new InvalidOperationException("Удалить можно только акты в статусе \"Драфт\"");

        // Сначала удаляем строки инвентаризации
        db.InventoryLines.RemoveRange(inventory.InventoryLines);
        
        // Затем удаляем сам акт
        db.Inventories.Remove(inventory);
        
        db.SaveChanges();
    }

    private string GenerateDocumentNumber(DocumentType type)
    {
        var prefix = type switch
        {
            DocumentType.WriteOff => "СПИ",
            DocumentType.Incoming => "ПР",
            _ => "ДОК"
        };
        var date = DateTime.Now;
        // No DB call! Just use a timestamp + random for super simple numbering
        return $"{prefix}-{date:MMyy}-{date.Ticks % 10000:D4}";
    }
}
