using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using System.Collections.ObjectModel;


namespace PharmacyWarehouse.Services
{
    public class CategoryService
    {
        private readonly PharmacyWarehouseContext _db = BaseDbService.Instance.Context;
        public ObservableCollection<Category> Categories { get; set; } = new();

        public CategoryService()
        {
            GetAll();
        }

        public int Commit() => _db.SaveChanges();

        public void Add(Category category)
        {
            var _category = new Category
            {
                Name = category.Name,
                Description = category.Description
            };

            _db.Categories.Add(_category);
            if (Commit() > 0)
                Categories.Add(_category);
        }

        public void GetAll()
        {
            var categories = _db.Categories
                .Include(c => c.Products)
                .ToList();

            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }

        public void Remove(Category category)
        {
            var existing = _db.Categories.Find(category.Id);
            if (existing != null)
            {
                _db.Categories.Remove(existing);
                if (Commit() > 0)
                    if (Categories.Contains(category))
                        Categories.Remove(category);
            }
        }

        public Category? GetById(int id)
        {
            return _db.Categories
                .Include(c => c.Products)
                .FirstOrDefault(c => c.Id == id);
        }

        public ObservableCollection<Category> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Categories;
            }

            var filtered = _db.Categories
                .Include(c => c.Products)
                .Where(c => c.Name.Contains(searchTerm) ||
                           (c.Description != null && c.Description.Contains(searchTerm)))
                .ToList();

            var result = new ObservableCollection<Category>(filtered);
            return result;
        }

        public void Update(Category category)
        {
            var existing = _db.Categories.Find(category.Id);
            if (existing != null)
            {
                existing.Name = category.Name;
                existing.Description = category.Description;

                _db.Categories.Update(existing);
                Commit();
            }
        }
    }
}