using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Models.Reports;

namespace PharmacyWarehouse.Services;

public class ReportService
{
    public List<StockReportRow> GetStockReport(DateTime date)
    {
        using var db = new PharmacyWarehouseContext();
        var reportDate = DateOnly.FromDateTime(date);

        return db.Batches
            .Include(b => b.Product)
                .ThenInclude(p => p.Category)
            .Where(b => b.ArrivalDate <= reportDate && b.Quantity > 0 && b.IsActive)
            .OrderBy(b => b.Product.Name)
            .Select(b => new StockReportRow
            {
                ProductName = b.Product.Name,
                CategoryName = b.Product.Category != null ? b.Product.Category.Name : "-",
                Manufacturer = b.Product.Manufacturer,
                Series = b.Series,
                ExpirationDate = b.ExpirationDate,
                Quantity = b.Quantity,
                SellingPrice = b.SellingPrice
            })
            .ToList();
    }

    public List<MovementReportRow> GetMovementReport(DateTime from, DateTime to)
    {
        using var db = new PharmacyWarehouseContext();
        var fromDate = from.Date;
        var toDate = to.Date;

        var lines = db.DocumentLines
            .Include(l => l.Product)
            .Include(l => l.Document)
            .Where(l => l.Document.Status == DocumentStatus.Processed
                     && l.Document.Date >= fromDate
                     && l.Document.Date <= toDate)
            .ToList();

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();

        var allProducts = db.Products
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Batches)
            .ToList();

        var result = new List<MovementReportRow>();

        foreach (var productId in productIds)
        {
            var product = allProducts.First(p => p.Id == productId);
            var productLines = lines.Where(l => l.ProductId == productId).ToList();

            var incomingQty = productLines
                .Where(l => l.Document.Type == DocumentType.Incoming)
                .Sum(l => l.Quantity);

            var outgoingQty = productLines
                .Where(l => l.Document.Type == DocumentType.Outgoing)
                .Sum(l => l.Quantity);

            var writeOffQty = productLines
                .Where(l => l.Document.Type == DocumentType.WriteOff)
                .Sum(l => l.Quantity);

            var currentStock = (int)product.CurrentStock;
            var openingBalance = currentStock - incomingQty + outgoingQty + writeOffQty;
            var closingBalance = openingBalance + incomingQty - outgoingQty - writeOffQty;

            result.Add(new MovementReportRow
            {
                ProductName = product.Name,
                IncomingQty = incomingQty,
                OutgoingQty = outgoingQty,
                WriteOffQty = writeOffQty,
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance
            });
        }

        return result.OrderBy(r => r.ProductName).ToList();
    }

    public List<SalesAnalysisRow> GetSalesAnalysis(DateTime from, DateTime to, int topN = 10)
    {
        using var db = new PharmacyWarehouseContext();
        var fromDate = from.Date;
        var toDate = to.Date;

        var sales = db.DocumentLines
            .Include(l => l.Product)
            .Include(l => l.Document)
            .Where(l => l.Document.Type == DocumentType.Outgoing
                     && l.Document.Status == DocumentStatus.Processed
                     && l.Document.Date >= fromDate
                     && l.Document.Date <= toDate)
            .GroupBy(l => new { l.ProductId, l.Product.Name })
            .Select(g => new
            {
                g.Key.Name,
                TotalQuantitySold = g.Sum(l => l.Quantity),
                TotalRevenue = g.Sum(l => l.Quantity * (l.SellingPrice ?? l.UnitPrice))
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        if (topN > 0)
            sales = sales.Take(topN).ToList();

        return sales.Select((s, index) => new SalesAnalysisRow
        {
            Rank = index + 1,
            ProductName = s.Name,
            TotalQuantitySold = s.TotalQuantitySold,
            TotalRevenue = s.TotalRevenue
        }).ToList();
    }

    public List<Inventory> GetCompletedInventories()
    {
        using var db = new PharmacyWarehouseContext();
        return db.Inventories
            .Where(i => i.Status == "Completed")
            .OrderByDescending(i => i.InventoryDate)
            .ToList();
    }

    public List<InventoryReportRow> GetInventoryReport(int inventoryId)
    {
        using var db = new PharmacyWarehouseContext();
        return db.InventoryLines
            .Include(l => l.Batch)
                .ThenInclude(b => b.Product)
            .Where(l => l.InventoryId == inventoryId)
            .Select(l => new InventoryReportRow
            {
                ProductName = l.Batch.Product.Name,
                Series = l.Batch.Series,
                ExpirationDate = l.Batch.ExpirationDate,
                AccountedQuantity = l.ExpectedQuantity,
                ActualQuantity = l.ActualQuantity ?? 0,
                Difference = (l.ActualQuantity ?? 0) - l.ExpectedQuantity
            })
            .ToList();
    }
}
