using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Windows;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PharmacyWarehouse.Pages;

public partial class SuppliersPage : Page
{
    private readonly SupplierService _supplierService;
    private ICollectionView _suppliersView;
    private List<Supplier> _filteredSuppliers = new();
    private List<Supplier> _currentPageSuppliers = new();
    private List<Supplier> _allSuppliers = new();
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages = 1;
    private bool _isPageLoaded = false;

    public SuppliersPage()
    {
        InitializeComponent();
        _supplierService = App.ServiceProvider.GetService<SupplierService>();
        DataContext = this;
        Loaded += OnPageLoaded;
    }

    #region Initialization

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        LoadSuppliers();
        _isPageLoaded = true;
        ApplyFilters();

        var isAdmin = AuthService.IsAdmin();
        Admin.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

    }

    private void LoadSuppliers()
    {
        try
        {
            _supplierService.GetAll();
            _allSuppliers = _supplierService.Suppliers.ToList();

            SetupSuppliersView();
            UpdateStatistics();
            UpdatePageInfo();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки", ex.Message);
        }
    }

    private void SetupSuppliersView()
    {
        _suppliersView = CollectionViewSource.GetDefaultView(_allSuppliers);
        _suppliersView.Filter = FilterSupplier;
        ApplyFilters();
    }

    private void RefreshSuppliers_Click(object sender, RoutedEventArgs e)
    {
        LoadSuppliers();
        ShowInfo("Список поставщиков обновлен");
    }

    #endregion

    #region Filtering

    private bool FilterSupplier(object obj)
    {
        if (obj is not Supplier supplier) return false;

        // Поиск по тексту
        if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
        {
            var searchText = SearchTextBox.Text.ToLower();
            if (!supplier.Name.ToLower().Contains(searchText) &&
                !(supplier.ContactPerson?.ToLower().Contains(searchText) ?? false) &&
                !(supplier.Phone?.ToLower().Contains(searchText) ?? false) &&
                !(supplier.Inn?.ToLower().Contains(searchText) ?? false) &&
                !(supplier.Address?.ToLower().Contains(searchText) ?? false))
                return false;
        }

        // Фильтр по статусу активности
        if (cmbStatus.SelectedItem is ComboBoxItem statusItem)
        {
            string statusText = statusItem.Content.ToString();
            switch (statusText)
            {
                case "Активные":
                    if (!supplier.IsActive) return false;
                    break;
                case "Неактивные":
                    if (supplier.IsActive) return false;
                    break;
            }
        }

        // Фильтр по наличию партий
        if (cmbHasBatches.SelectedItem is ComboBoxItem batchesItem)
        {
            string batchesText = batchesItem.Content.ToString();
            switch (batchesText)
            {
                case "С партиями":
                    if (supplier.BatchesCount == 0) return false;
                    break;
                case "Без партий":
                    if (supplier.BatchesCount > 0) return false;
                    break;
            }
        }

        // Фильтр по активности за месяц
        if (cmbRecentActivity.SelectedItem is ComboBoxItem recentItem)
        {
            string recentText = recentItem.Content.ToString();
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var hasRecentDocuments = supplier.Documents?.Any(d => d.Date >= monthStart) ?? false;

            switch (recentText)
            {
                case "Были поставки":
                    if (!hasRecentDocuments) return false;
                    break;
                case "Без поставок":
                    if (hasRecentDocuments) return false;
                    break;
            }
        }

        // Фильтр только активные
        if (chkShowOnlyActive.IsChecked == true && !supplier.IsActive)
            return false;

        // Фильтр по просроченным поставкам
        if (chkHasLateDeliveries.IsChecked == true && !supplier.HasLateDeliveries)
            return false;

        return true;
    }

    private void ApplyFilters()
    {
        _suppliersView?.Refresh();

        _filteredSuppliers = _suppliersView?.Cast<Supplier>().ToList() ?? new List<Supplier>();

        _currentPage = 1;

        UpdateStatistics();
        UpdatePageInfo();
        UpdateDataGrid();
    }

    private void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void UpdateDataGrid()
    {
        try
        {
            var startIndex = (_currentPage - 1) * _pageSize;
            _currentPageSuppliers = _filteredSuppliers
                .Skip(startIndex)
                .Take(_pageSize > 0 ? _pageSize : _filteredSuppliers.Count)
                .ToList();

            dgSuppliers.ItemsSource = null;
            dgSuppliers.ItemsSource = _currentPageSuppliers;
            dgSuppliers.UpdateLayout();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка обновления таблицы", ex.Message);
        }
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        cmbStatus.SelectedIndex = 0;
        cmbHasBatches.SelectedIndex = 0;
        cmbRecentActivity.SelectedIndex = 0;
        chkShowOnlyActive.IsChecked = true;
        chkHasLateDeliveries.IsChecked = false;

        ApplyFilters();
        ShowInfo("Фильтры сброшены");
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ApplyFilters();
    }

    private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void CmbHasBatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void CmbRecentActivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void ChkShowOnlyActive_Checked(object sender, RoutedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void ChkShowOnlyActive_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void ChkHasLateDeliveries_Checked(object sender, RoutedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    private void ChkHasLateDeliveries_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isPageLoaded)
            ApplyFilters();
    }

    #endregion

    #region CRUD Operations

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEditSupplierWindow();
        if (dialog.ShowDialog() == true)
        {
            LoadSuppliers();
        }
    }

    private void EditSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not Supplier selectedSupplier)
        {
            ShowInfo("Выберите поставщика для редактирования");
            return;
        }

        var dialog = new AddEditSupplierWindow(selectedSupplier);
        if (dialog.ShowDialog() == true)
        {
            LoadSuppliers();
        }
    }

    private void DeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not Supplier selectedSupplier)
        {
            ShowInfo("Выберите поставщика для удаления");
            return;
        }

        if (selectedSupplier.Batches.Any() || selectedSupplier.Documents.Any())
        {
            ShowError("Удаление невозможно",
                "Нельзя удалить поставщика, у которого есть связанные партии или документы.");
            return;
        }

        var result = MessageBox.Show(
            $"Удалить поставщика '{selectedSupplier.Name}'?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _supplierService.Remove(selectedSupplier);
                LoadSuppliers();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка удаления", ex.Message);
            }
        }
    }

    private void DeactivateSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not Supplier selectedSupplier)
        {
            ShowInfo("Выберите поставщика для деактивации");
            return;
        }

        var result = MessageBox.Show(
            $"Деактивировать поставщика '{selectedSupplier.Name}'?\n\n" +
            "Поставщик будет скрыт из списка при создании новых заказов.",
            "Деактивация поставщика",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                selectedSupplier.IsActive = false;
                _supplierService.Update(selectedSupplier);
                LoadSuppliers();
                ShowInfo($"Поставщик '{selectedSupplier.Name}' деактивирован");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка деактивации", ex.Message);
            }
        }
    }

    private void ActivateSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not Supplier selectedSupplier)
        {
            ShowInfo("Выберите поставщика для активации");
            return;
        }

        var result = MessageBox.Show(
            $"Активировать поставщика '{selectedSupplier.Name}'?\n\n" +
            "Поставщик снова будет доступен для заказа.",
            "Активация поставщика",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                selectedSupplier.IsActive = true;
                _supplierService.Update(selectedSupplier);
                LoadSuppliers();
                ShowInfo($"Поставщик '{selectedSupplier.Name}' активирован");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка активации", ex.Message);
            }
        }
    }

    private void DgSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedSupplierInfo();

        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            btnDeactivate.Visibility = supplier.IsActive ? Visibility.Visible : Visibility.Collapsed;
            btnActivate.Visibility = !supplier.IsActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void DgSuppliers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditSupplier_Click(sender, e);
    }

    #endregion

    #region Statistics and Info

    private void UpdateStatistics()
    {
        if (!_isPageLoaded) return;

        try
        {
            var filteredSuppliers = _suppliersView?.Cast<Supplier>().ToList() ?? new List<Supplier>();

            int total = filteredSuppliers.Count;
            int active = filteredSuppliers.Count(s => s.IsActive);
            int inactive = filteredSuppliers.Count(s => !s.IsActive);
            int withBatches = filteredSuppliers.Count(s => s.BatchesCount > 0);
            int withoutBatches = filteredSuppliers.Count(s => s.BatchesCount == 0);
            int withLateDeliveries = filteredSuppliers.Count(s => s.HasLateDeliveries);

            // Подсчет приходных документов за месяц
            int monthlyDeliveries = filteredSuppliers
                .Sum(s => s.MonthlyDeliveriesCount);

            // Подсчет общего количества документов
            int totalDocuments = filteredSuppliers
                .Sum(s => s.DocumentsCount);

            // Подсчет среднего времени - используем количество документов как показатель
            double avgDocumentsPerSupplier = filteredSuppliers.Any()
                ? filteredSuppliers.Average(s => s.DocumentsCount)
                : 0;

            txtTotalCount.Text = $"Всего: {total}";
            txtActiveCount.Text = $"Активных: {active}";
            txtInactiveCount.Text = $"Неактивных: {inactive}";
            txtWithBatchesCount.Text = $"С партиями: {withBatches}";
            txtWithoutBatchesCount.Text = $"Без партий: {withoutBatches}";
            txtMonthlyDeliveries.Text = $"Приходов за месяц: {monthlyDeliveries}";
            txtLateDeliveries.Text = $"Просрочек: {withLateDeliveries}";
        }
        catch (Exception ex)
        {
            ShowError("Ошибка статистики", ex.Message);
        }
    }

    private void UpdateSelectedSupplierInfo()
    {
        if (dgSuppliers.SelectedItem is not Supplier selectedSupplier)
        {
            txtSelectedSupplier.Text = "Не выбран поставщик";
            txtSupplierContact.Text = "-";
            txtSupplierPhone.Text = "-";
            txtSupplierAddress.Text = "-";
            txtSupplierInn.Text = "-";
            pnlBankDetails.Visibility = Visibility.Collapsed;
            dgRecentDeliveries.ItemsSource = null;
            return;
        }

        // Обновляем контактную информацию
        txtSelectedSupplier.Text = selectedSupplier.Name;
        txtSupplierContact.Text = selectedSupplier.ContactPerson ?? "-";
        txtSupplierPhone.Text = selectedSupplier.Phone ?? "-";
        txtSupplierAddress.Text = selectedSupplier.Address ?? "-";
        txtSupplierInn.Text = selectedSupplier.Inn ?? "-";

        // Обновляем банковские реквизиты (если есть)
        bool hasBankDetails = !string.IsNullOrWhiteSpace(selectedSupplier.BankName) ||
                             !string.IsNullOrWhiteSpace(selectedSupplier.BankAccount);
        pnlBankDetails.Visibility = hasBankDetails ? Visibility.Visible : Visibility.Collapsed;

        if (hasBankDetails)
        {
            txtBankName.Text = $"Банк: {selectedSupplier.BankName ?? "-"}";
            txtBankAccount.Text = $"Счет: {selectedSupplier.BankAccount ?? "-"}";
        }

        // Обновляем последние поставки
        UpdateRecentDeliveries(selectedSupplier);
    }

    private void UpdateRecentDeliveries(Supplier supplier)
    {
        try
        {
            if (supplier.Documents == null || !supplier.Documents.Any())
            {
                dgRecentDeliveries.ItemsSource = null;
                return;
            }

            var recentDeliveries = supplier.Documents
                .Where(d => d.Type == DocumentType.Incoming)
                .OrderByDescending(d => d.Date)
                .Take(10)
                .Select(d => new
                {
                    Date = d.Date.ToString("dd.MM.yyyy"),
                    DocumentNumber = d.Number ?? "Без номера",
                    Amount = d.Amount?.ToString("N2") ?? "0,00",
                    Status = GetDocumentStatus(d)
                })
                .ToList();

            dgRecentDeliveries.ItemsSource = recentDeliveries.Any() ? recentDeliveries : null;

            if (!recentDeliveries.Any())
            {
                dgRecentDeliveries.ItemsSource = new List<object>
        {
            new { Date = "", DocumentNumber = "Нет приходных документов", Amount = "", Status = "" }
        };
            }
        }
        catch (Exception ex)
        {
            ShowError("Ошибка обновления поставок", ex.Message);
        }
    }

    private string GetDocumentStatus(Document document)
    {
        if (document == null) return "Неизвестно";

        switch (document.Status)
        {
            case DocumentStatus.Processed:
                return "Проведен";
            case DocumentStatus.Draft:
                if (document.Type == DocumentType.Incoming &&
                    document.SupplierInvoiceDate.HasValue &&
                    document.SupplierInvoiceDate.Value < DateTime.Now.Date)
                    return "Просрочен";
                return "Черновик";
            case DocumentStatus.Blocked:
                return "Заблокирован";
            default:
                return "Неизвестно";
        }
    }
    #endregion

    #region Pagination

    private void UpdatePageInfo()
    {
        if (!_isPageLoaded) return;

        var filteredCount = _suppliersView?.Cast<Supplier>().Count() ?? 0;
        _totalPages = _pageSize > 0 ? (int)Math.Ceiling(filteredCount / (double)_pageSize) : 1;

        txtTotalPages.Text = _totalPages.ToString();
        txtCurrentPage.Text = _currentPage.ToString();
    }

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
            UpdatePageInfo();
        }

    }

    private void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _currentPage = 1;
        UpdateDataGrid();
        UpdatePageInfo();
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            UpdateDataGrid();
            UpdatePageInfo();
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            UpdateDataGrid();
            UpdatePageInfo();
        }
    }

    private void LastPage_Click(object sender, RoutedEventArgs e)
    {
        _currentPage = _totalPages;
        UpdateDataGrid();
        UpdatePageInfo();
    }

    private void TxtCurrentPage_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (int.TryParse(txtCurrentPage.Text, out int page) &&
            page >= 1 && page <= _totalPages &&
            page != _currentPage)
        {
            _currentPage = page;
            UpdateDataGrid();
            UpdatePageInfo();
        }
    }

    #endregion

    #region Context Menu Actions

    private void ViewBatches_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            ShowInfo($"Просмотр партий поставщика: {supplier.Name}");
        }
    }

    private void ViewDocuments_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            ShowInfo($"Просмотр документов поставщика: {supplier.Name}");
        }
    }

    private void CreateDelivery_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            ShowInfo($"Создание поставки от: {supplier.Name}");
        }
    }

    private void CreateOrder_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            ShowInfo($"Создание заказа для: {supplier.Name}");
        }
    }

    private void CallSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier && !string.IsNullOrWhiteSpace(supplier.Phone))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"tel:{supplier.Phone}",
                    UseShellExecute = true
                });
            }
            catch
            {
                ShowInfo($"Телефон для звонка: {supplier.Phone}");
            }
        }
    }

    private void EmailSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            ShowInfo($"Email поставщика: {supplier.Name}\nФункция отправки email будет реализована позже");
        }
    }

    private void CopyName_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier)
        {
            Clipboard.SetText(supplier.Name);
            ShowInfo($"Название скопировано: {supplier.Name}");
        }
    }

    private void CopyInn_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is Supplier supplier && !string.IsNullOrWhiteSpace(supplier.Inn))
        {
            Clipboard.SetText(supplier.Inn);
            ShowInfo($"ИНН скопирован: {supplier.Inn}");
        }
    }

    // Быстрые действия из нижней панели
    private void QuickCreateDelivery_Click(object sender, RoutedEventArgs e)
    {
        CreateDelivery_Click(sender, e);
    }

    private void QuickCreateOrder_Click(object sender, RoutedEventArgs e)
    {
        CreateOrder_Click(sender, e);
    }

    private void QuickCallSupplier_Click(object sender, RoutedEventArgs e)
    {
        CallSupplier_Click(sender, e);
    }

    #endregion

    #region Helper Methods

    private void ShowInfo(string message)
    {
        MessageBox.Show(message, "Информация",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowError(string title, string message)
    {
        MessageBox.Show(message, title,
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #endregion
}
