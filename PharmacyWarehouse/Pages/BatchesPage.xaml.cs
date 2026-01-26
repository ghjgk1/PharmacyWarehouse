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

    private List<Batch> _filteredBatches = new List<Batch>();
    private List<Batch> _currentPageBatches = new List<Batch>();
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages = 1;
    private bool _isPageLoaded = false;

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
        _isPageLoaded = false;
        LoadData();
        _isPageLoaded = true;
        ApplyFilters();

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
            // Применяем пагинацию к отфильтрованным данным
            var startIndex = (_currentPage - 1) * _pageSize;
            _currentPageBatches = _filteredBatches
                .Skip(startIndex)
                .Take(_pageSize > 0 ? _pageSize : _filteredBatches.Count)
                .ToList();

            dgBatches.ItemsSource = null;
            dgBatches.ItemsSource = _currentPageBatches;
            dgBatches.UpdateLayout();

            // Обновляем информацию о страницах
            UpdatePageInfo();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при обновлении таблицы: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdatePageInfo()
    {
        if (!_isPageLoaded) return;

        try
        {
            _totalPages = _pageSize > 0 ?
                (int)Math.Ceiling(_filteredBatches.Count / (double)_pageSize) : 1;
            if (_totalPages == 0) _totalPages = 1;

            if (_currentPage > _totalPages)
            {
                _currentPage = _totalPages;
            }

            txtTotalPages.Text = _totalPages.ToString();
            txtCurrentPage.Text = _currentPage.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в UpdatePageInfo: {ex.Message}");
        }
    }

    private void UpdateFilterStatistics(List<Batch> batches)
    {
        try
        {
            int total = batches.Count;
            int active = batches.Count(b => b.IsActive && b.Quantity > 0);
            int expired = batches.Count(b => b.IsExpired && b.IsActive && b.Quantity > 0);
            int expiring = batches.Count(b => b.IsExpiringSoon && b.IsActive && b.Quantity > 0);

            txtTotalBatches.Text = $"Всего партий: {total}";
            txtActiveBatches.Text = $"Активных: {active}";
            txtExpiredBatches.Text = $"Просроченных: {expired}";
            txtExpiringBatches.Text = $"Истекающих: {expiring}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в UpdateFilterStatistics: {ex.Message}");
        }
    }

    private void UpdateTotals()
    {
        try
        {
            var batches = _filteredBatches;
            int totalCount = batches.Count;
            int totalQuantity = batches.Sum(b => b.Quantity);
            decimal totalValue = batches.Sum(b => b.TotalPurchaseValue);

            txtTotalCount.Text = totalCount.ToString();
            txtTotalQuantity.Text = totalQuantity.ToString("N0");
            txtTotalValue.Text = totalValue.ToString("N2");

            // Обновляем статистику фильтров
            UpdateFilterStatistics(batches);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в UpdateTotals: {ex.Message}");
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

            _filteredBatches = filteredBatches.ToList();

            _currentPage = 1;

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

    #endregion

    #region Pagination Methods

    private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded) return;

        if (cmbPageSize.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is string tag &&
            int.TryParse(tag, out int newSize))
        {
            _pageSize = newSize;
            _currentPage = 1;
            UpdateDataGrid();
        }
    }

    private void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _currentPage = 1;
        UpdateDataGrid();
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            UpdateDataGrid();
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            UpdateDataGrid();
        }
    }

    private void LastPage_Click(object sender, RoutedEventArgs e)
    {
        _currentPage = _totalPages;
        UpdateDataGrid();
    }

    private void TxtCurrentPage_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isPageLoaded) return;

        if (int.TryParse(txtCurrentPage.Text, out int page) &&
            page >= 1 && page <= _totalPages &&
            page != _currentPage)
        {
            _currentPage = page;
            UpdateDataGrid();
        }
    }

    #endregion

}