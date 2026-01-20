using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using System;
using System.ComponentModel;
using System.Windows;

namespace PharmacyWarehouse.Windows;

public partial class AddEditSupplierWindow : Window, INotifyPropertyChanged
{
    private readonly SupplierService _service;
    private bool _isEditMode = false;

    private Supplier _supplier;
    public Supplier Supplier
    {
        get => _supplier;
        set
        {
            _supplier = value;
            OnPropertyChanged(nameof(Supplier));
        }
    }

    public string WindowTitle => _isEditMode ? $"Редактирование: {Supplier?.Name}" : "Новый поставщик";

    public event PropertyChangedEventHandler PropertyChanged;

    public AddEditSupplierWindow(Supplier supplier = null)
    {
        InitializeComponent();
        DataContext = this;
        _service = App.ServiceProvider.GetService<SupplierService>();

        if (supplier != null && supplier.Id > 0)
        {
            _isEditMode = true;
            var existingSupplier = _service.GetById(supplier.Id);
            if (existingSupplier != null)
            {
                Supplier = new Supplier
                {
                    Id = existingSupplier.Id,
                    Name = existingSupplier.Name,
                    Inn = existingSupplier.Inn,
                    Phone = existingSupplier.Phone,
                    ContactPerson = existingSupplier.ContactPerson,
                    Address = existingSupplier.Address,
                    BankName = existingSupplier.BankName,
                    BankAccount = existingSupplier.BankAccount,
                    IsActive = existingSupplier.IsActive
                };
            }
            else
            {
                Supplier = new Supplier();
            }
        }
        else
        {
            // Устанавливаем значения по умолчанию для NOT NULL полей
            Supplier = new Supplier
            {
                IsActive = true,
                Inn = "",
                BankAccount = "",
                BankName = "",
                Phone = "",
                ContactPerson = "",
                Address = ""
            };
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Валидация обязательных полей
        if (string.IsNullOrWhiteSpace(Supplier.Name))
        {
            MessageBox.Show("Введите название поставщика", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Валидация ИНН - должно быть заполнено (NOT NULL в БД)
        if (string.IsNullOrWhiteSpace(Supplier.Inn))
        {
            MessageBox.Show("Введите ИНН поставщика", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Supplier.Inn.Length != 10 && Supplier.Inn.Length != 12)
        {
            MessageBox.Show("ИНН должен содержать 10 или 12 цифр", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Проверка остальных NOT NULL полей
        if (string.IsNullOrWhiteSpace(Supplier.BankAccount))
        {
            MessageBox.Show("Введите расчетный счет", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Supplier.BankName))
        {
            MessageBox.Show("Введите название банка", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Supplier.Phone))
        {
            MessageBox.Show("Введите телефон поставщика", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Supplier.ContactPerson))
        {
            MessageBox.Show("Введите контактное лицо", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Supplier.Address))
        {
            MessageBox.Show("Введите адрес поставщика", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_isEditMode)
            {
                var existing = _service.GetById(Supplier.Id);
                if (existing != null)
                {
                    existing.Name = Supplier.Name;
                    existing.Inn = Supplier.Inn;
                    existing.Phone = Supplier.Phone;
                    existing.ContactPerson = Supplier.ContactPerson;
                    existing.Address = Supplier.Address;
                    existing.BankName = Supplier.BankName;
                    existing.BankAccount = Supplier.BankAccount;
                    existing.IsActive = Supplier.IsActive;

                    _service.Update(existing);
                }
            }
            else
            {
                _service.Add(Supplier);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}