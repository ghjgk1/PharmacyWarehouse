using System.Collections.ObjectModel;
using PharmacyWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;

namespace PharmacyWarehouse.Services
{
    public class BatchService
    {
        private readonly PharmacyWarehouseContext _db = BaseDbService.Instance.Context;
        public ObservableCollection<Batch> Batches { get; set; } = new();

        public BatchService()
        {
            GetAll();
        }

        public int Commit() => _db.SaveChanges();


        public void GetAll()
        {
            var batches = _db.Batches
                .Include(b => b.Product)
                .Include(b => b.Supplier)
                .Include(b => b.IncomingDocument)
                .OrderByDescending(b => b.ArrivalDate)
                .ToList();

            Batches.Clear();
            foreach (var batch in batches)
            {
                Batches.Add(batch);
            }
        }

        public Batch? GetById(int id)
        {
            return _db.Batches
                .Include(b => b.Product)
                .Include(b => b.Supplier)
                .Include(b => b.IncomingDocument)
                .Include(b => b.DocumentLines)
                    .ThenInclude(dl => dl.Document)
                .FirstOrDefault(b => b.Id == id);
        }

        public List<Batch> GetByProduct(int productId)
        {
            return _db.Batches
                .Where(b => b.ProductId == productId)
                .Include(b => b.Supplier)
                .Include(b => b.IncomingDocument)
                .OrderBy(b => b.ExpirationDate)
                .ToList();
        }


        public ObservableCollection<Batch> GetExpiredBatches()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var batches = _db.Batches
                .Where(b => b.ExpirationDate <= today && b.Quantity > 0 && b.IsActive)
                .Include(b => b.Product)
                .Include(b => b.Supplier)
                .Include(b => b.IncomingDocument)
                .ToList();

            var collection = new ObservableCollection<Batch>();
            foreach (var batch in batches)
            {
                collection.Add(batch);
            }
            return collection;
        }

        public ObservableCollection<Batch> GetExpiringBatches(int days = 30)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var warningDate = today.AddDays(days);

            var batches = _db.Batches
                .Where(b => b.ExpirationDate > today &&
                           b.ExpirationDate <= warningDate &&
                           b.Quantity > 0 &&
                           b.IsActive)
                .Include(b => b.Product)
                .Include(b => b.Supplier)
                .Include(b => b.IncomingDocument)
                .ToList();

            var collection = new ObservableCollection<Batch>();
            foreach (var batch in batches)
            {
                collection.Add(batch);
            }
            return collection;
        }

        public ObservableCollection<Batch> GetActiveBatches()
        {
            var batches = _db.Batches
                .Where(b => b.Quantity > 0 && b.IsActive)
                .Include(b => b.Product)
                .Include(b => b.Supplier)
                .Include(b => b.IncomingDocument)
                .ToList();

            var collection = new ObservableCollection<Batch>();
            foreach (var batch in batches)
            {
                collection.Add(batch);
            }
            return collection;
        }



        public int GetBatchRemainingQuantity(int batchId)
        {
            var batch = _db.Batches
                .AsNoTracking()
                .FirstOrDefault(b => b.Id == batchId);
            if (batch == null) return 0;

            var totalWriteOff = _db.DocumentLines
                .Where(line => line.SourceBatchId == batchId)
                .Sum(line => (int?)line.Quantity) ?? 0; 

            return batch.Quantity - totalWriteOff;
        }
    }
}
