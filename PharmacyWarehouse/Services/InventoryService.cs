using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;

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
            }
        }

        db.SaveChanges();
    }

    public void CompleteInventory(int inventoryId)
    {
        using var db = new PharmacyWarehouseContext();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            var inventory = db.Inventories
                .Include(i => i.InventoryLines)
                    .ThenInclude(l => l.Batch)
                        .ThenInclude(b => b.Product)
                .FirstOrDefault(i => i.Id == inventoryId);

            if (inventory == null)
                throw new InvalidOperationException("Акт инвентаризации не найден");

            if (inventory.Status == "Completed")
                throw new InvalidOperationException("Инвентаризация уже завершена");

            var diffLines = inventory.InventoryLines
                .Where(l => l.ActualQuantity.HasValue && l.Difference != 0)
                .ToList();

            Document? writeOffDoc = null;
            decimal writeOffAmount = 0;

            if (diffLines.Any(l => l.Difference < 0))
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
                    Notes = $"Инвентаризация {inventory.Number}",
                    SignedBy = _currentUser,
                    SignedAt = DateTime.Now
                };
                db.Documents.Add(writeOffDoc);
                db.SaveChanges();
            }

            foreach (var line in diffLines)
            {
                var batch = line.Batch;
                var oldQty = batch.Quantity;
                var newQty = line.ActualQuantity!.Value;

                batch.Quantity = newQty;
                if (batch.Quantity == 0)
                    batch.IsActive = false;

                if (line.Difference < 0 && writeOffDoc != null)
                {
                    var qty = Math.Abs(line.Difference);
                    writeOffAmount += qty * batch.PurchasePrice;

                    db.BatchCorrectionLogs.Add(new BatchCorrectionLog
                    {
                        BatchId = batch.Id,
                        CorrectionDocumentId = writeOffDoc.Id,
                        CorrectionDate = DateTime.Now,
                        FieldName = "Quantity",
                        OldValue = oldQty.ToString(),
                        NewValue = newQty.ToString(),
                        ChangedBy = _currentUser,
                        Reason = $"Инвентаризация {inventory.Number}"
                    });

                    db.DocumentLines.Add(new DocumentLine
                    {
                        DocumentId = writeOffDoc.Id,
                        ProductId = batch.ProductId,
                        Quantity = qty,
                        UnitPrice = batch.PurchasePrice,
                        Series = batch.Series,
                        ExpirationDate = batch.ExpirationDate,
                        SourceBatchId = batch.Id,
                        Notes = $"Инвентаризационная разница: {oldQty} → {newQty}"
                    });
                }
            }

            if (writeOffDoc != null)
                writeOffDoc.Amount = writeOffAmount;

            inventory.Status = "Completed";
            inventory.CompletedAt = DateTime.Now;
            db.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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
        return db.Inventories
            .Include(i => i.InventoryLines)
                .ThenInclude(l => l.Batch)
                    .ThenInclude(b => b.Product)
            .AsNoTracking()
            .FirstOrDefault(i => i.Id == id);
    }

    private string GenerateDocumentNumber(DocumentType type)
    {
        using var db = new PharmacyWarehouseContext();
        var prefix = type switch
        {
            DocumentType.WriteOff => "СПИ",
            _ => "ДОК"
        };
        var date = DateTime.Now;
        var count = db.Documents.Count(d => d.Type == type
            && d.CreatedAt.Month == date.Month
            && d.CreatedAt.Year == date.Year);
        return $"{prefix}-{date:MMyy}-{count + 1:D4}";
    }
}
