using System.Security.Cryptography;
using System.Text;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Data;

namespace PharmacyWarehouse.Services;

public class AuthService
{
    private static User? _currentUser;
    public static User? CurrentUser => _currentUser;

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    // Метод создания пользователя - только для админа
    public bool CreateUser(string fullName, string login, string password, UserRole role)
    {
        // Проверяем, что текущий пользователь - администратор
        if (CurrentUser?.Role != UserRole.Admin)
            return false;

        using var db = new PharmacyWarehouseContext();

        if (db.Users.Any(u => u.Login == login))
            return false;

        var user = new User
        {
            FullName = fullName,
            Login = login,
            PasswordHash = HashPassword(password),
            Role = role,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        db.Users.Add(user);
        db.SaveChanges();

        return true;
    }

    // Вход
    public User? Login(string login, string password)
    {
        using var db = new PharmacyWarehouseContext();
        var passwordHash = HashPassword(password);

            var user = db.Users
            .FirstOrDefault(u => u.Login == login &&
                                u.PasswordHash == passwordHash &&
                                u.IsActive);

        if (user != null)
        {
            _currentUser = user;
        }

        return user;
    }

    // Выход
    public void Logout()
    {
        _currentUser = null;
    }

    // Создание администратора по умолчанию если нет пользователей
    public void CreateDefaultAdmin()
    {
        using var db = new PharmacyWarehouseContext();

        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                FullName = "Администратор",
                Login = "admin",
                PasswordHash = HashPassword("admin123"),
                Role = UserRole.Admin,
                CreatedAt = DateTime.Now,
                IsActive = true
            });

            db.SaveChanges();
        }
    }

    // Проверка является ли админом
    public static bool IsAdmin()
    {
        return CurrentUser?.Role == UserRole.Admin;
    }
}