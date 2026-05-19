using System;
using System.ComponentModel.DataAnnotations;

namespace PharmacyWarehouse.Models;

public enum UserRole
{
    Admin,        
    Pharmacist,   
    Manager
}

public partial class User : ObservableObject
{
    private int _id;
    private string _fullName = null!;
    private string _login = null!;
    private string _passwordHash = null!;
    private UserRole _role = UserRole.Pharmacist;
    private DateTime _createdAt = DateTime.Now;
    private bool _isActive = true;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string PasswordHash
    {
        get => _passwordHash;
        set => SetProperty(ref _passwordHash, value);
    }

    public UserRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}