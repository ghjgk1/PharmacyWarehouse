using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services;

public class NotificationService
{
    public (List<Product> lowStock, List<Product> expiring, List<Product> expired) GetNotifications()
    {
        // Создаем НОВЫЙ экземпляр DbContext, чтобы избежать кэширования
        using var db = new PharmacyWarehouseContext();
        
        // Загружаем товары и их активные партии
        var products = db.Products
            .Where(p => p.IsActive)
            .Include(p => p.Batches.Where(b => b.IsActive))
            .AsNoTracking() // Отключаем отслеживание для свежих данных
            .ToList();

        var lowStock = products.Where(p => 
            p.Batches.Sum(b => b.Quantity) > 0 && 
            p.Batches.Sum(b => b.Quantity) <= p.MinRemainder)
            .ToList();
        
        var expiring = products.Where(p => p.HasExpiringBatches).ToList();
        var expired = products.Where(p => p.HasExpiredBatches).ToList();

        return (lowStock, expiring, expired);
    }
}
