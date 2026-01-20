using System.Collections.ObjectModel;
using PharmacyWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;

namespace PharmacyWarehouse.Services
{

    public class SupplierService
    {
        private readonly PharmacyWarehouseContext _db = BaseDbService.Instance.Context;
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();

        public SupplierService()
        {
            GetAll();
        }

        public int Commit() => _db.SaveChanges();

        public void Add(Supplier supplier)
        {
            var _supplier = new Supplier
            {
                Name = supplier.Name,
                Phone = supplier.Phone,
                ContactPerson = supplier.ContactPerson,
                IsActive = supplier.IsActive,
                Address = supplier.Address
            };

            _db.Suppliers.Add(_supplier);
            if (Commit() > 0)
                Suppliers.Add(_supplier);
        }

        public void GetAll()
        {
            var suppliers = _db.Suppliers
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
            var existing = _db.Suppliers.Find(supplier.Id);
            if (existing != null)
            {
                _db.Suppliers.Remove(existing);
                if (Commit() > 0)
                    if (Suppliers.Contains(supplier))
                        Suppliers.Remove(supplier);
            }
        }

        public Supplier? GetById(int id)
        {
            return _db.Suppliers
                .Include(s => s.Batches)
                .Include(s => s.Documents)
                .FirstOrDefault(s => s.Id == id);
        }

        public void Update(Supplier supplier)
        {
            var existing = _db.Suppliers.Find(supplier.Id);
            if (existing != null)
            {
                existing.Name = supplier.Name;
                existing.Phone = supplier.Phone;
                existing.ContactPerson = supplier.ContactPerson;
                existing.Address = supplier.Address;
                existing.IsActive = supplier.IsActive;

                if (Commit() > 0)
                {
                    var index = Suppliers.IndexOf(Suppliers.FirstOrDefault(s => s.Id == supplier.Id));
                    if (index >= 0)
                    {
                        Suppliers[index] = existing;
                    }
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
