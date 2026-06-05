using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Models.Reports;

namespace PharmacyWarehouse.Services;

public class DashboardService
{
    public DashboardStats GetStats()
    {
        using var db = new PharmacyWarehouseContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var warningDate = today.AddDays(30);

        var products = db.Products
            .Where(p => p.IsActive)
            .Include(p => p.Batches.Where(b => b.IsActive))
            .AsNoTracking()
            .ToList();

        var batches = db.Batches
            .Where(b => b.IsActive && b.Quantity > 0)
            .AsNoTracking()
            .ToList();

        return new DashboardStats
        {
            TotalActiveProducts = products.Count,
            TotalActiveBatches = batches.Count,
            ExpiredBatchesCount = batches.Count(b => b.ExpirationDate < today),
            LowStockCount = products.Count(p => 
                p.Batches.Sum(b => b.Quantity) > 0 && 
                p.Batches.Sum(b => b.Quantity) <= p.MinRemainder),
            ExpiringBatchesCount = batches.Count(b =>
                b.ExpirationDate >= today && b.ExpirationDate <= warningDate)
        };
    }

    public List<TopSaleItem> GetTopSales(int days = 30, int topN = 5)
    {
        using var db = new PharmacyWarehouseContext();
        var fromDate = DateTime.Now.AddDays(-days).Date;

        return db.DocumentLines
            .Include(l => l.Product)
            .Include(l => l.Document)
            .Where(l => l.Document.Type == DocumentType.Outgoing
                     && l.Document.Status == DocumentStatus.Processed
                     && l.Document.Date >= fromDate)
            .AsNoTracking()
            .GroupBy(l => new { l.ProductId, l.Product.Name })
            .Select(g => new TopSaleItem
            {
                ProductName = g.Key.Name,
                TotalQuantity = g.Sum(l => l.Quantity),
                TotalRevenue = g.Sum(l => l.Quantity * (l.SellingPrice ?? l.UnitPrice))
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(topN)
            .ToList();
    }

    public List<StockDynamicsPoint> GetStockDynamics(int days = 30)
    {
        using var db = new PharmacyWarehouseContext();
        var result = new List<StockDynamicsPoint>();
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        var endDate = DateOnly.FromDateTime(DateTime.Today);

        var lines = db.DocumentLines
            .Include(l => l.Document)
            .Where(l => l.Document.Status == DocumentStatus.Processed
                     && l.Document.Date >= startDate.ToDateTime(TimeOnly.MinValue)
                     && l.Document.Date <= endDate.ToDateTime(TimeOnly.MaxValue))
            .AsNoTracking()
            .ToList();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = date.ToDateTime(TimeOnly.MaxValue);

            var dayLines = lines.Where(l => l.Document.Date >= dayStart && l.Document.Date <= dayEnd);

            result.Add(new StockDynamicsPoint
            {
                Date = date,
                IncomeQty = dayLines.Where(l => l.Document.Type == DocumentType.Incoming).Sum(l => l.Quantity),
                SaleQty = dayLines.Where(l => l.Document.Type == DocumentType.Outgoing).Sum(l => l.Quantity),
                WriteOffQty = dayLines.Where(l => l.Document.Type == DocumentType.WriteOff).Sum(l => l.Quantity)
            });
        }

        return result;
    }
}
