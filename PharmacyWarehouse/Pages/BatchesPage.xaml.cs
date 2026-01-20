using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PharmacyWarehouse.Pages;

public partial class BatchesPage : Page
{
    public BatchService Service { get; set; }
    public Batch SelectedBatch { get; set; }
    public Batch SearchBatch { get; set; } = new Batch();
    private ProductService _productService;
    private SupplierService _supplierService;
    private List<Batch> _allBatches = new List<Batch>();

    public BatchesPage()
    {
        InitializeComponent();
        Service = App.ServiceProvider.GetService<BatchService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _supplierService = App.ServiceProvider.GetService<SupplierService>();

        Service.GetAll();
        _productService.GetAll();
        _supplierService.GetAll();

        Loaded += BatchesPage_Loaded;
    }

    private void BatchesPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            _productService.GetAll();
            _supplierService.GetAll();

            _allBatches = Service.Batches.ToList();

            LoadProducts();
            LoadSuppliers();

            if (cmbBatchStatus != null)
            {
                cmbBatchStatus.SelectionChanged += CmbBatchStatus_SelectionChanged;
            }

            DataContext = this;

            ApplyFilters();

            UpdateFilterStatistics(_allBatches);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDataGrid()
    {
        try
        {
            dgBatches.ItemsSource = null;
            dgBatches.ItemsSource = Service.Batches;
            dgBatches.UpdateLayout();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при обновлении таблицы: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadProducts()
    {
        try
        {
            cmbProduct.Items.Clear();

            var allProductsItem = new { Id = 0, Name = "Все товары" };
            cmbProduct.Items.Add(allProductsItem);

            foreach (var product in _productService.Products)
            {
                cmbProduct.Items.Add(product);
            }

            cmbProduct.DisplayMemberPath = "Name";
            cmbProduct.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSuppliers()
    {
        try
        {
            cmbSupplier.Items.Clear();

            var allSuppliersItem = new { Id = 0, Name = "Все поставщики" };
            cmbSupplier.Items.Add(allSuppliersItem);

            foreach (var supplier in _supplierService.Suppliers)
            {
                cmbSupplier.Items.Add(supplier);
            }

            cmbSupplier.DisplayMemberPath = "Name";
            cmbSupplier.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке поставщиков: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilters();
            UpdateSelectedBatchInfo();
        }
    }

    private void CmbSupplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilters();
            UpdateSelectedBatchInfo();
        }
    }

    private void CmbBatchStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilters();
            UpdateSelectedBatchInfo();
        }
    }

    private void UpdateSelectedBatchInfo()
    {
        if (SelectedBatch != null)
        {
            var existsInFiltered = Service.Batches.Any(b => b.Id == SelectedBatch.Id);

            if (existsInFiltered)
            {
                UpdateBatchDetails(SelectedBatch);
            }
            else
            {
                SelectedBatch = null;
                ClearBatchDetails();
            }
        }
        else
        {
            ClearBatchDetails();
        }
    }

    // Очистка информации о партии
    private void ClearBatchDetails()
    {
        txtSelectedBatch.Text = "Партия не выбрана";
        txtBatchProduct.Text = "Товар: -";
        txtBatchSupplier.Text = "Поставщик: -";
        txtBatchExpiration.Text = "Срок годности: -";
        txtBatchQuantity.Text = "Остаток: -";
        txtBatchPrice.Text = "Цена закупки: -";

    }

    // Обновление статистики фильтров
    private void UpdateFilterStatistics(List<Batch> filteredBatches)
    {
        try
        {
            int total = filteredBatches.Count;
            int active = filteredBatches.Count(b => b.IsActive && b.Quantity > 0);
            int expired = filteredBatches.Count(b => b.IsExpired && b.IsActive && b.Quantity > 0);
            int expiring = filteredBatches.Count(b => b.IsExpiringSoon && b.IsActive && b.Quantity > 0);

            txtTotalBatches.Text = $"Всего партий: {total}";
            txtActiveBatches.Text = $"Активных: {active}";
            txtExpiredBatches.Text = $"Просроченных: {expired}";
            txtExpiringBatches.Text = $"Истекающих: {expiring}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при обновлении статистики: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Обновленный метод обновления деталей партии
    private void UpdateBatchDetails(Batch batch)
    {
        try
        {
            var fullBatch = Service.GetById(batch.Id);
            if (fullBatch != null)
            {
                txtSelectedBatch.Text = $"Партия: {fullBatch.Series}";
                txtBatchProduct.Text = $"Товар: {fullBatch.Product?.Name ?? "-"}";
                txtBatchSupplier.Text = $"Поставщик: {fullBatch.Supplier?.Name ?? "-"}";
                txtBatchExpiration.Text = $"Срок годности: {fullBatch.ExpirationDate:dd.MM.yyyy}";
                txtBatchQuantity.Text = $"Остаток: {fullBatch.Quantity:N0}";
                txtBatchPrice.Text = $"Цена закупки: {fullBatch.PurchasePrice:N2} руб.";

            }
            else
            {
                ClearBatchDetails();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при обновлении деталей: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region CRUD Operations

    private void WriteOffBatch_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedBatch == null)
        {
            MessageBox.Show("Выберите партию для списания", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SelectedBatch.Quantity <= 0)
        {
            MessageBox.Show("В выбранной партии нет товара для списания", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        }

    private void ShowExpiredBatches_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var expiredBatches = Service.GetExpiredBatches();
            Service.Batches.Clear();
            foreach (var batch in expiredBatches)
            {
                Service.Batches.Add(batch);
            }

            UpdateDataGrid();
            UpdateTotals();

            MessageBox.Show($"Показано {expiredBatches.Count} просроченных партий",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при фильтрации: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowExpiringBatches_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var expiringBatches = Service.GetExpiringBatches();
            Service.Batches.Clear();
            foreach (var batch in expiringBatches)
            {
                Service.Batches.Add(batch);
            }

            UpdateDataGrid();
            UpdateTotals();

            MessageBox.Show($"Показано {expiringBatches.Count} партий с истекающим сроком",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при фильтрации: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Filtering Methods

    private void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        try
        {
            var filteredBatches = _allBatches.AsEnumerable();

            // Фильтр по товару
            if (cmbProduct.SelectedItem != null)
            {
                var selectedItem = cmbProduct.SelectedItem;

                var nameProperty = selectedItem.GetType().GetProperty("Name");
                if (nameProperty != null)
                {
                    string itemName = nameProperty.GetValue(selectedItem)?.ToString();

                    if (itemName != "Все товары")
                    {
                        var idProperty = selectedItem.GetType().GetProperty("Id");
                        if (idProperty != null)
                        {
                            var idValue = idProperty.GetValue(selectedItem);
                            if (idValue != null && idValue is int productId)
                            {
                                filteredBatches = filteredBatches.Where(b => b.ProductId == productId);
                            }
                        }
                        else if (selectedItem is Product product)
                        {
                            filteredBatches = filteredBatches.Where(b => b.ProductId == product.Id);
                        }
                    }
                }
            }

            // Фильтр по поставщику
            if (cmbSupplier.SelectedItem != null)
            {
                var selectedItem = cmbSupplier.SelectedItem;
                var type = selectedItem.GetType();
                var idProperty = type.GetProperty("Id");

                if (idProperty != null)
                {
                    var idValue = idProperty.GetValue(selectedItem);
                    if (idValue != null && idValue is int supplierId && supplierId > 0)
                    {
                        filteredBatches = filteredBatches.Where(b => b.SupplierId == supplierId);
                    }
                }
                else if (selectedItem is Supplier supplier)
                {
                    filteredBatches = filteredBatches.Where(b => b.SupplierId == supplier.Id);
                }
            }

            // Фильтр по статусу
            if (cmbBatchStatus.SelectedItem is ComboBoxItem statusItem)
            {
                string statusText = statusItem.Content.ToString();

                filteredBatches = statusText switch
                {
                    "Активные" => filteredBatches.Where(b => b.IsActive && b.Quantity > 0),
                    "Просроченные" => filteredBatches.Where(b => b.IsExpired && b.IsActive && b.Quantity > 0),
                    "Истекающие" => filteredBatches.Where(b => b.IsExpiringSoon && b.IsActive && b.Quantity > 0),
                    "Списаны" => filteredBatches.Where(b => !b.IsActive || b.Quantity == 0),
                    _ => filteredBatches
                };
            }

            // Преобразуем в список
            var filteredList = filteredBatches.ToList();

            // Обновляем коллекцию
            Service.Batches.Clear();
            foreach (var batch in filteredList)
            {
                Service.Batches.Add(batch);
            }

            // Обновляем DataGrid
            UpdateDataGrid();
            UpdateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при применении фильтров: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (cmbProduct != null)
            {
                var allProductsItem = cmbProduct.Items
                    .Cast<object>()
                    .FirstOrDefault(item =>
                        item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() == "Все товары");
                cmbProduct.SelectedItem = allProductsItem;
            }

            if (cmbSupplier != null)
            {
                var allSuppliersItem = cmbSupplier.Items
                    .Cast<object>()
                    .FirstOrDefault(item =>
                        item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() == "Все поставщики");
                cmbSupplier.SelectedItem = allSuppliersItem;
            }

            if (cmbBatchStatus != null)
            {
                cmbBatchStatus.SelectedIndex = 0;
            }

            SearchBatch.Series = string.Empty;

            // Обновляем привязку данных
            var bindingExpression = GetBindingExpression(DataContextProperty);
            if (bindingExpression != null)
            {
                bindingExpression.UpdateTarget();
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при сбросе фильтров: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    #endregion

    #region DataGrid Events

    private void DgBatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgBatches.SelectedItem is Batch selected)
        {
            SelectedBatch = selected;
            UpdateBatchDetails(selected);
        }
        else
        {
            SelectedBatch = null;
            ClearBatchDetails();
        }
    }


    private void UpdateTotals()
    {
        try
        {
            var batches = Service.Batches;
            int totalCount = batches.Count;
            int totalQuantity = batches.Sum(b => b.Quantity);
            decimal totalValue = batches.Sum(b => b.TotalPurchaseValue);

            txtTotalCount.Text = totalCount.ToString();
            txtTotalQuantity.Text = totalQuantity.ToString("N0");
            txtTotalValue.Text = totalValue.ToString("N2");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при обновлении итогов: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Button Events (для существующих кнопок в XAML)

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Экспорт данных будет реализован позже",
            "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BatchReport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Отчет по партиям будет реализован позже",
            "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion
}