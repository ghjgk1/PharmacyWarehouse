using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using System.Collections.ObjectModel;


namespace PharmacyWarehouse.Services
{
    public class CategoryService
    {
        public ObservableCollection<Category> Categories { get; set; } = new();

        public CategoryService()
        {
            GetAll();
        }

        public void Add(Category category)
        {
            using var db = new PharmacyWarehouseContext();
            var _category = new Category
            {
                Name = category.Name,
                Description = category.Description
            };

            db.Categories.Add(_category);
            if (db.SaveChanges() > 0)
            {
                GetAll();
            }
        }

        public void GetAll()
        {
            using var db = new PharmacyWarehouseContext();
            var categories = db.Categories
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
            using var db = new PharmacyWarehouseContext();
            var existing = db.Categories.Find(category.Id);
            if (existing != null)
            {
                db.Categories.Remove(existing);
                if (db.SaveChanges() > 0)
                {
                    GetAll();
                }
            }
        }

        public Category? GetById(int id)
        {
            using var db = new PharmacyWarehouseContext();
            return db.Categories
                .Include(c => c.Products)
                .FirstOrDefault(c => c.Id == id);
        }

        public ObservableCollection<Category> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Categories;
            }

            using var db = new PharmacyWarehouseContext();
            var filtered = db.Categories
                .Include(c => c.Products)
                .Where(c => c.Name.Contains(searchTerm) ||
                           (c.Description != null && c.Description.Contains(searchTerm)))
                .ToList();

            var result = new ObservableCollection<Category>(filtered);
            return result;
        }

        public void Update(Category category)
        {
            using var db = new PharmacyWarehouseContext();
            var existing = db.Categories.Find(category.Id);
            if (existing != null)
            {
                existing.Name = category.Name;
                existing.Description = category.Description;

                db.Categories.Update(existing);
                db.SaveChanges();
                GetAll();
            }
        }
    }
}