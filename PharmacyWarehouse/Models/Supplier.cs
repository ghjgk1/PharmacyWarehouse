using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWarehouse.Models;

public partial class Supplier : ObservableObject
{
    private int _id;
    private string _name = String.Empty;
    private string _inn = String.Empty;
    private string _bankAccount = String.Empty;
    private string _bankName = String.Empty;
    private string _phone = String.Empty;
    private string _contactPerson = String.Empty;
    private string _address = String.Empty;
    private bool _isActive = true;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Inn
    {
        get => _inn;
        set => SetProperty(ref _inn, value);
    }

    public string BankAccount
    {
        get => _bankAccount;
        set => SetProperty(ref _bankAccount, value);
    }

    public string BankName
    {
        get => _bankName;
        set => SetProperty(ref _bankName, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string ContactPerson
    {
        get => _contactPerson;
        set => SetProperty(ref _contactPerson, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }


    public virtual ICollection<Batch> Batches { get; set; } = new ObservableCollection<Batch>();
    public virtual ICollection<Document> Documents { get; set; } = new ObservableCollection<Document>();

    [NotMapped]
    public int BatchesCount => Batches?.Count ?? 0;

    [NotMapped]
    public int DocumentsCount => Documents?.Count ?? 0;

    [NotMapped]
    public string StatusText => IsActive ? "Активен" : "Неактивен";

    [NotMapped]
    public bool HasLateDeliveries
    {
        get
        {
            if (Documents == null) return false;

            return Documents.Any(d =>
                d.Type == DocumentType.Incoming &&
                d.SupplierInvoiceDate.HasValue &&
                d.SupplierInvoiceDate.Value < DateTime.Now.Date &&
                d.Status != DocumentStatus.Processed);
        }
    }

    [NotMapped]
    public int MonthlyDeliveriesCount
    {
        get
        {
            if (Documents == null) return 0;
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return Documents.Count(d =>
                d.Date >= monthStart &&
                d.Type == DocumentType.Incoming);
        }
    }

    [NotMapped]
    public bool IsPaid
    {
        get
        {
            if (Documents == null) return true;
            return Documents.All(d => d.Status == DocumentStatus.Processed);
        }
    }

    [NotMapped]
    public decimal TotalDeliveriesAmount
    {
        get
        {
            if (Documents == null) return 0;
            return Documents
                .Where(d => d.Type == DocumentType.Incoming)
                .Sum(d => d.TotalAmount);
        }
    }

    [NotMapped]
    public int RecentDeliveriesCount
    {
        get
        {
            if (Documents == null) return 0;
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            return Documents.Count(d =>
                d.Type == DocumentType.Incoming &&
                d.Date >= thirtyDaysAgo);
        }
    }

    [NotMapped]
    public decimal AverageDeliveryAmount
    {
        get
        {
            var incomingDocs = Documents?
                .Where(d => d.Type == DocumentType.Incoming && d.Amount.HasValue)
                .ToList();

            if (incomingDocs == null || !incomingDocs.Any())
                return 0;

            return incomingDocs.Average(d => d.Amount.Value);
        }
    }

    [NotMapped]
    public DateTime? LastDeliveryDate
    {
        get
        {
            return Documents?
                .Where(d => d.Type == DocumentType.Incoming)
                .OrderByDescending(d => d.Date)
                .Select(d => d.Date)
                .FirstOrDefault();
        }
    }
}