using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PharmacyWarehouse.Models;

public partial class Category : ObservableObject
{
    private int _id;
    private string _name = null!;
    private string? _description;

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

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public virtual ICollection<Product> Products { get; set; } = new ObservableCollection<Product>();
}
