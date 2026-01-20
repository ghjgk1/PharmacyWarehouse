using System;
using System.ComponentModel.DataAnnotations;

namespace PharmacyWarehouse.Models;

public enum UserRole
{
    Admin,        // Администратор - полный доступ
    Pharmacist,   // Фармацевт - продажи, просмотр остатков
}

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Pharmacist;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}