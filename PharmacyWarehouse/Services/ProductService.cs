using System.Collections.ObjectModel;
using PharmacyWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;

namespace PharmacyWarehouse.Services
{
    public class ProductService
    {
        private readonly PharmacyWarehouseContext _db = BaseDbService.Instance.Context;
        public ObservableCollection<Product> Products { get; set; } = new();

        public ProductService()
        {
            GetAll();
        }

        public int Commit() => _db.SaveChanges();

        public void Add(Product product)
        {
            var _product = new Product
            {
                Name = product.Name,
                CategoryId = product.CategoryId,
                ReleaseForm = product.ReleaseForm,
                Manufacturer = product.Manufacturer,
                UnitOfMeasure = product.UnitOfMeasure,
                MinRemainder = product.MinRemainder,
                RequiresPrescription = product.RequiresPrescription,
                IsActive = product.IsActive,
                IsSalesBlocked = product.IsSalesBlocked,
                ArchiveDate = product.ArchiveDate,
                ArchiveReason = product.ArchiveReason,
                ArchiveComment = product.ArchiveComment,
                Description = product.Description,
            };

            _db.Products.Add(_product);
            if (Commit() > 0)
                Products.Add(_product);
        }
        public void GetAll()
        {
            var products = _db.Products
                .Include(p => p.Category)
                .Include(p => p.Batches)
                .ToList();

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }
        }

        public void Remove(Product product)
        {
            var productWithBatches = _db.Products
                .Include(p => p.Batches)
                .FirstOrDefault(p => p.Id == product.Id);

            if (productWithBatches == null) return;

            if (productWithBatches.Batches.Any())
            {
                throw new InvalidOperationException(
                    "Нельзя удалить товар, у которого есть партии. Сначала удалите или спишите все партии.");
            }

            _db.Products.Remove(productWithBatches);
            _db.SaveChanges();
            GetAll();
        }

        public Product? GetById(int id)
        {
            return _db.Products
                .Include(p => p.Category)
                .Include(p => p.Batches)
                .Include(p => p.DocumentLines)
                .FirstOrDefault(p => p.Id == id);
        }
        public void Update(Product product)
        {
            var existing = _db.Products.Find(product.Id);
            if (existing == null) return;

            existing.Name = product.Name;
            existing.CategoryId = product.CategoryId;
            existing.ReleaseForm = product.ReleaseForm;
            existing.Manufacturer = product.Manufacturer;
            existing.UnitOfMeasure = product.UnitOfMeasure;
            existing.MinRemainder = product.MinRemainder;
            existing.RequiresPrescription = product.RequiresPrescription;
            existing.IsActive = product.IsActive;
            existing.IsSalesBlocked = product.IsSalesBlocked;
            existing.ArchiveDate = product.ArchiveDate;
            existing.ArchiveReason = product.ArchiveReason;
            existing.ArchiveComment = product.ArchiveComment;
            existing.Description = product.Description;

            _db.SaveChanges();
            GetAll();
        }

        public List<Product> GetProductsByStatus(string status)
        {
            return status switch
            {
                "С низким остатком" => Products.Where(p => p.IsLowStock).ToList(),
                "Нет в наличии" => Products.Where(p => p.IsOutOfStock).ToList(),
                "Истекает срок" => Products.Where(p => p.HasExpiringBatches).ToList(),
                "Просроченные" => Products.Where(p => p.HasExpiredBatches).ToList(),
                _ => Products.ToList()
            };
        }

        public void     ArchiveProduct(int productId, string reason, bool blockSales,
                          string comment = null)
        {
            var product = _db.Products
                .Include(p => p.Batches)
                .FirstOrDefault(p => p.Id == productId);

            if (product == null) return;

            product.IsActive = false;
            product.IsSalesBlocked = blockSales;
            product.ArchiveDate = DateTime.Now;
            product.ArchiveReason = reason;
            product.ArchiveComment = comment;

            var archiveNote = $"\n---\n[АРХИВ] {DateTime.Now:dd.MM.yyyy HH:mm}" +
                             $"\nПричина: {reason}" +
                             $"\nОстаток: {product.CurrentStock} шт." +
                             $"\nСтатус: {(blockSales ? "Заблокирован" : "Дораспродажа")}";

            if (!string.IsNullOrEmpty(comment))
                archiveNote += $"\nКомментарий: {comment}";

            _db.SaveChanges();
            GetAll();
        }

        public void ActivateProduct(int productId)
        {
            var product = _db.Products.Find(productId);
            if (product == null) return;

            product.IsActive = true;
            product.IsSalesBlocked = false;
            product.ArchiveDate = null;

            _db.SaveChanges();
            GetAll();
        }

        public bool CanSellProduct(int productId)
        {
            var product = _db.Products.Find(productId);
            return product != null && !product.IsSalesBlocked;
        }

        public bool CanOrderProduct(int productId)
        {
            var product = _db.Products.Find(productId);
            return product != null && product.IsActive;
        }
    }
}
