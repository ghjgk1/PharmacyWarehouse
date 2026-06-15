using System.Collections.ObjectModel;
using PharmacyWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;

namespace PharmacyWarehouse.Services
{

    public class SupplierService
    {
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();

        public SupplierService()
        {
            GetAll();
        }

        public void Add(Supplier supplier)
        {
            using var db = new PharmacyWarehouseContext();
            var _supplier = new Supplier
            {
                Name = supplier.Name,
                Phone = supplier.Phone,
                ContactPerson = supplier.ContactPerson,
                IsActive = supplier.IsActive,
                Address = supplier.Address
            };

            db.Suppliers.Add(_supplier);
            if (db.SaveChanges() > 0)
            {
                GetAll();
            }
        }

        public void GetAll()
        {
            using var db = new PharmacyWarehouseContext();
            var suppliers = db.Suppliers
                .Include(s => s.Batches)
                .Include(s => s.Documents)
                .ToList();

            Suppliers.Clear();
            foreach (var supplier in suppliers)
            {
                Suppliers.Add(supplier);
            }
        }

        public void Remove(Supplier supplier)
        {
            using var db = new PharmacyWarehouseContext();
            var existing = db.Suppliers.Find(supplier.Id);
            if (existing != null)
            {
                db.Suppliers.Remove(existing);
                if (db.SaveChanges() > 0)
                    GetAll();
            }
        }

        public Supplier? GetById(int id)
        {
            using var db = new PharmacyWarehouseContext();
            return db.Suppliers
                .Include(s => s.Batches)
                .Include(s => s.Documents)
                .FirstOrDefault(s => s.Id == id);
        }

        public void Update(Supplier supplier)
        {
            using var db = new PharmacyWarehouseContext();
            var existing = db.Suppliers.Find(supplier.Id);
            if (existing != null)
            {
                existing.Name = supplier.Name;
                existing.Phone = supplier.Phone;
                existing.ContactPerson = supplier.ContactPerson;
                existing.Address = supplier.Address;
                existing.IsActive = supplier.IsActive;

                if (db.SaveChanges() > 0)
                {
                    GetAll();
                }
            }
        }

        public ObservableCollection<Supplier> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Suppliers;

            var filtered = Suppliers.Where(s =>
                s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (s.Phone != null && s.Phone.Contains(searchTerm)) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (s.Address != null && s.Address.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return new ObservableCollection<Supplier>(filtered);
        }
    }
}
