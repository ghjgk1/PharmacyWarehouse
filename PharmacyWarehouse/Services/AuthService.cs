using System.Security.Cryptography;
using System.Text;
using PharmacyWarehouse.Models;

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

        var context = BaseDbService.Instance.Context;

        if (context.Users.Any(u => u.Login == login))
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

        context.Users.Add(user);
        context.SaveChanges();

        return true;
    }

    // Вход
    public User? Login(string login, string password)
    {
        var context = BaseDbService.Instance.Context;
        var passwordHash = HashPassword(password);

        var user = context.Users
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
        var context = BaseDbService.Instance.Context;

        if (!context.Users.Any())
        {
            context.Users.Add(new User
            {
                FullName = "Администратор",
                Login = "admin",
                PasswordHash = HashPassword("admin123"),
                Role = UserRole.Admin,
                CreatedAt = DateTime.Now,
                IsActive = true
            });

            context.SaveChanges();
        }
    }

    // Проверка является ли админом
    public static bool IsAdmin()
    {
        return CurrentUser?.Role == UserRole.Admin;
    }
}