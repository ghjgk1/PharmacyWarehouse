using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Windows;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PharmacyWarehouse.Pages
{
    public partial class ProductsPage : Page
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly AuthService _authService;
        private ICollectionView _productsView;
        private List<Product> _allProducts = new();
        private int _currentPage = 1;
        private int _pageSize = 25;
        private int _totalPages = 1; 
        private string _currentUser = String.Empty;

        private bool _isPageLoaded = false;

        public ProductsPage()
        {
            _authService = App.ServiceProvider.GetService<AuthService>();

            _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";

            InitializeComponent();
            _productService = App.ServiceProvider.GetService<ProductService>();
            _categoryService = App.ServiceProvider.GetService<CategoryService>();
            DataContext = this;
            Loaded += OnPageLoaded;
        }

        #region Initialization

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            _isPageLoaded = false;
            LoadProducts();
            _isPageLoaded = true;
            ApplyFilters();

            var isAdmin = AuthService.IsAdmin();
            Admin.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadProducts()
        {
            try
            {
                _productService.GetAll();
                _allProducts = _productService.Products.ToList();

                SetupProductsView();
                LoadCategories();
                UpdateStatistics();
                UpdatePageInfo();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки", ex.Message);
            }
        }

        private void SetupProductsView()
        {
            _productsView = CollectionViewSource.GetDefaultView(_allProducts);
            _productsView.Filter = FilterProduct;
        }

        #endregion

        #region Filtering

        private bool FilterProduct(object obj)
        {
            if (obj is not Product product) return false;

            // Поиск по тексту
            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                var searchText = SearchTextBox.Text.ToLower();
                if (!product.Name.ToLower().Contains(searchText) &&
                    !(product.Manufacturer?.ToLower().Contains(searchText) ?? false) &&
                    !(product.Description?.ToLower().Contains(searchText) ?? false))
                    return false;
            }

            // Фильтр по категории
            if (cmbCategory.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is int categoryId)
            {
                if (categoryId > 0 && product.CategoryId != categoryId)
                    return false;
            }


            switch (cmbPrescription.SelectedIndex)
            {
                case 1:
                    if (product.RequiresPrescription != true) return false;
                    break;
                case 2:
                    if (product.RequiresPrescription == true) return false;
                    break;
            }

            switch (cmbAvailability.SelectedIndex)
            {
                case 1:
                    if (!product.IsInStock) return false;
                    break;
                case 2:
                    if (!product.IsOutOfStock) return false;
                    break;
                case 3:
                    if (!product.IsLowStock) return false;
                    break;
            }

            if (chkExpiringSoon.IsChecked == true && !product.HasExpiringBatches)
                return false;

            if (chkExpired.IsChecked == true && !product.HasExpiredBatches)
                return false;

            if (chkShowArchived.IsChecked != true && !product.IsActive)
                return false;

            return true;
        }

        private void ApplyFilters()
        {
            if (!_isPageLoaded || dgProducts == null) return;

            _productsView?.Refresh();
            UpdateStatistics();
            UpdatePageInfo();

            var currentPageProducts = GetCurrentPageProducts();
            dgProducts.ItemsSource = currentPageProducts;
        }

        private List<Product> GetCurrentPageProducts()
        {
            var filteredProducts = _productsView?.Cast<Product>().ToList() ?? new List<Product>();

            if (filteredProducts.Count == 0)
                return new List<Product>();

            if (_currentPage < 1) _currentPage = 1;

            var startIndex = (_currentPage - 1) * _pageSize;

            if (startIndex >= filteredProducts.Count)
            {
                _currentPage = 1;
                startIndex = 0;
            }

            var pageProducts = filteredProducts
                .Skip(startIndex)
                .Take(_pageSize > 0 ? _pageSize : int.MaxValue)
                .ToList();

            return pageProducts;
        }

        #endregion

        #region CRUD Operations

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            OpenProductDialog(null);
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is not Product selectedProduct)
            {
                ShowInfo("Выберите товар для редактирования");
                return;
            }

            OpenProductDialog(selectedProduct);
        }

        private void OpenProductDialog(Product product = null)
        {
            var dialog = product == null
                ? new AddEditProductWindow()
                : new AddEditProductWindow(product);

            if (dialog.ShowDialog() == true)
            {
                LoadProducts();
            }
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is not Product selectedProduct)
            {
                ShowInfo("Выберите товар для удаления");
                return;
            }

            if (selectedProduct.Batches.Any())
            {
                ShowError("Удаление невозможно", "Нельзя удалить товар, у которого есть партии.");
                return;
            }

            var result = MessageBox.Show(
                $"Удалить товар '{selectedProduct.Name}'?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _productService.Remove(selectedProduct);
                    LoadProducts();
                }
                catch (Exception ex)
                {
                    ShowError("Ошибка удаления", ex.Message);
                }
            }
        }

        private void ArchiveProduct_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is not Product selectedProduct)
            {
                ShowInfo("Выберите товар для архивации");
                return;
            }


            var dialog = new ArchiveProductWindow(selectedProduct);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var archiveInfo = dialog.Result;

                    bool blockSales;

                    if (selectedProduct.CurrentStock == 0)
                    {
                        blockSales = archiveInfo.RequiresWriteOff ? true : false;
                        ExecuteArchive(selectedProduct, archiveInfo, blockSales);
                    }
                    else if (archiveInfo.RequiresWriteOff)
                    {
                        var writeOffResult = CreateWriteOffForArchive(selectedProduct, archiveInfo);

                        if (writeOffResult.Success)
                        {
                            blockSales = true;
                            ExecuteArchive(selectedProduct, archiveInfo, blockSales);

                            ShowInfo($"Товар '{selectedProduct.Name}' списан и архивирован. " +
                                    $"Документ списания: {writeOffResult.DocumentNumber}");

                            var documentService = App.ServiceProvider.GetService<DocumentService>();
                            documentService.GetAll();
                        }
                        else
                        {
                            ShowError("Ошибка списания", writeOffResult.ErrorMessage);
                            return;
                        }
                    }
                    else
                    {
                        blockSales = false;

                        var confirmResult = MessageBox.Show(
                                           $"Перевести товар '{selectedProduct.Name}' в режим дораспродажи?\n\n" +
                                           $"• Заказ нового товара будет невозможен\n" +
                                           $"• Остатки ({selectedProduct.CurrentStock} шт.) будут доступны для продажи\n" +
                                           $"• Причина: {archiveInfo.Reason}",
                                           "Подтверждение дораспродажи",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Question);

                        if (confirmResult == MessageBoxResult.Yes)
                        {
                            ExecuteArchive(selectedProduct, archiveInfo, blockSales);
                            ShowInfo($"Товар '{selectedProduct.Name}' переведен в режим дораспродажи");
                        }
                        else
                        {
                            return;
                        }
                    }

                    LoadProducts();

                    btnArchive.Visibility = selectedProduct.IsActive ? Visibility.Visible : Visibility.Collapsed;
                    btnActivate.Visibility = !selectedProduct.IsActive ? Visibility.Visible : Visibility.Collapsed;

                }
                catch (Exception ex)
                {
                    ShowError("Ошибка архивации", ex.Message);
                }
            }
        }

        private void ActivateProduct_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is not Product selectedProduct)
            {
                ShowInfo("Выберите товар для активации");
                return;
            }

            var result = MessageBox.Show(
                $"Активировать товар '{selectedProduct.Name}'?\n\nТовар снова будет доступен для заказа и продажи.",
                "Активация товара",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _productService.ActivateProduct(selectedProduct.Id);
                    ShowInfo($"Товар '{selectedProduct.Name}' активирован");
                    LoadProducts();

                    btnArchive.Visibility = selectedProduct.IsActive ? Visibility.Visible : Visibility.Collapsed;
                    btnActivate.Visibility = !selectedProduct.IsActive ? Visibility.Visible : Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    ShowError("Ошибка активации", ex.Message);
                }
            }
        }
        private WriteOffResult CreateWriteOffForArchive(Product product, ArchiveInfo archiveInfo)
        {
            try
            {
                var documentService = App.ServiceProvider.GetService<DocumentService>();

                var document = new Document
                {
                    Type = DocumentType.WriteOff,
                    Date = DateTime.Now,
                    WriteOffReason = archiveInfo.WriteOffReason ?? WriteOffReason.Other,
                    Notes = $"Списание при архивации товара. Причина архивации: {archiveInfo.Reason}" +
                           (!string.IsNullOrEmpty(archiveInfo.Comment) ? $"\nКомментарий: {archiveInfo.Comment}" : ""),
                    CreatedBy = _currentUser,
                    CreatedAt = DateTime.Now,
                    Status = DocumentStatus.Draft
                };

                var documentLines = new List<DocumentLine>();
                foreach (var batch in product.Batches.Where(b => b.IsActive && b.Quantity > 0))
                {
                    var line = new DocumentLine
                    {
                        ProductId = product.Id,
                        Quantity = batch.Quantity,
                        UnitPrice = batch.SellingPrice,
                        SellingPrice = batch.SellingPrice,
                        Series = batch.Series,
                        ExpirationDate = batch.ExpirationDate,
                        Notes = $"Списание при архивации. Причина: {archiveInfo.Reason}",
                        SourceBatchId = batch.Id
                    };

                    documentLines.Add(line);
                }

                int documentId = documentService.CreateWriteOff(document, documentLines);

                var createdDocument = documentService.GetById(documentId);

                return new WriteOffResult
                {
                    Success = true,
                    DocumentId = documentId,
                    DocumentNumber = createdDocument?.Number ?? "Н/Д"
                };
            }
            catch (Exception ex)
            {
                return new WriteOffResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private void ExecuteArchive(Product product, ArchiveInfo archiveInfo, bool blockSales)
        {
            string fullComment = $"Причина архивации: {archiveInfo.Reason}";
            if (!string.IsNullOrEmpty(archiveInfo.Comment))
            {
                fullComment += $"\nКомментарий: {archiveInfo.Comment}";
            }
            if (archiveInfo.IsCritical)
            {
                fullComment += "\n[КРИТИЧЕСКАЯ ПРИЧИНА]";
            }
            if (archiveInfo.RequiresWriteOff)
            {
                fullComment += "\n[ОСТАТКИ СПИСАНЫ]";
            }

            _productService.ArchiveProduct(product.Id, archiveInfo.Reason, blockSales, fullComment);
        }

        #endregion

        #region UI Event Handlers

        private void DgProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgProducts.SelectedItem is Product product)
            {
                btnArchive.Visibility = product.IsActive ? Visibility.Visible : Visibility.Collapsed;
                btnActivate.Visibility = !product.IsActive ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void DgProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditProduct_Click(sender, e);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbPrescription_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbAvailability_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkShowArchived_Checked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkShowArchived_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkExpiringSoon_Checked(object sender, RoutedEventArgs e)
        {
            if (chkExpired.IsChecked == true && IsLoaded)
                chkExpired.IsChecked = false;
            ApplyFilters();
        }

        private void ChkExpiringSoon_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkExpired_Checked(object sender, RoutedEventArgs e)
        {
            if (chkExpiringSoon.IsChecked == true && IsLoaded)
                chkExpiringSoon.IsChecked = false;
            ApplyFilters();
        }

        private void ChkExpired_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            ResetFilters();
        }

        private void RefreshProducts_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            ShowInfo("Список товаров обновлен");
        }

        #endregion

        #region Helper Methods

        private void LoadCategories()
        {
            try
            {
                cmbCategory.Items.Clear();

                var allItem = new ComboBoxItem
                {
                    Content = "Все категории",
                    Tag = 0
                };
                cmbCategory.Items.Add(allItem);

                var categories = _categoryService.Categories;
                foreach (var category in categories)
                {
                    var item = new ComboBoxItem
                    {
                        Content = category.Name,
                        Tag = category.Id
                    };
                    cmbCategory.Items.Add(item);
                }

                cmbCategory.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки категорий", ex.Message);
            }
        }

        private void ResetFilters()
        {
            SearchTextBox.Text = string.Empty;
            cmbCategory.SelectedIndex = 0;
            cmbPrescription.SelectedIndex = 0;
            cmbAvailability.SelectedIndex = 0;
            chkShowArchived.IsChecked = false;
            chkExpiringSoon.IsChecked = false;
            chkExpired.IsChecked = false;

            _currentPage = 1;

            ApplyFilters();
            ShowInfo("Фильтры сброшены");
        }

        private void UpdateStatistics()
        {
            if (!_isPageLoaded) return;

            try
            {
                var filteredProducts = _allProducts.Where(p => FilterProduct(p)).ToList();

                int total = filteredProducts.Count;
                int inStock = filteredProducts.Count(p => p.IsInStock);
                int lowStock = filteredProducts.Count(p => p.IsLowStock);
                int outOfStock = filteredProducts.Count(p => p.IsOutOfStock);
                int expiring = filteredProducts.Count(p => p.HasExpiringBatches);
                int expired = filteredProducts.Count(p => p.HasExpiredBatches);

                txtTotalCount.Text = $"Всего: {total}";
                txtFilteredCount.Text = $"Найдено: {total}";
                txtInStockCount.Text = $"В наличии: {inStock}";
                txtLowStockCount.Text = $"Низкий остаток: {lowStock}";
                txtOutOfStockCount.Text = $"Нет в наличии: {outOfStock}";
                txtExpiringCount.Text = $"Истекает срок: {expiring}";
                txtExpiredCount.Text = $"Просрочено: {expired}";

            }
            catch (Exception ex)
            {
                ShowError("Ошибка статистики", ex.Message);
            }
        }

        private void UpdatePageInfo()
        {
            if (!_isPageLoaded) return;

            var filteredCount = _productsView?.Cast<Product>().Count() ?? 0;
            _totalPages = _pageSize > 0 ? (int)Math.Ceiling(filteredCount / (double)_pageSize) : 1;
            if (_totalPages == 0) _totalPages = 1;

            txtTotalPages.Text = _totalPages.ToString();
            txtCurrentPage.Text = _currentPage.ToString();
        }

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

        #region Pagination

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPageSize.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string tag &&
                int.TryParse(tag, out int newSize))
            {
                _pageSize = newSize;
                _currentPage = 1; 
                ApplyFilters();
            }
        }


        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            UpdatePageInfo();
            ApplyFilters();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePageInfo();
                ApplyFilters();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdatePageInfo();
                ApplyFilters();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = _totalPages;
            UpdatePageInfo();
            ApplyFilters();
        }

        private void TxtCurrentPage_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtCurrentPage.Text, out int page) &&
                page >= 1 && page <= _totalPages &&
                page != _currentPage)
            {
                _currentPage = page;
                UpdatePageInfo();
                ApplyFilters();
            }
        }

        #endregion

        #region Context Menu Actions

        private void ViewBatches_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is Product product)
            {
                ShowInfo($"Просмотр партий для товара: {product.Name}");
            }
        }

        private void CreateArrival_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is Product product)
            {
                ShowInfo($"Создание прихода для товара: {product.Name}");
            }
        }

        private void CreateWriteOff_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is Product product)
            {
                ShowInfo($"Создание списания для товара: {product.Name}");
            }
        }

        private void CopyName_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is Product product)
            {
                Clipboard.SetText(product.Name);
                ShowInfo($"Название скопировано: {product.Name}");
            }
        }

        private void CopyId_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is Product product)
            {
                Clipboard.SetText(product.Id.ToString());
                ShowInfo($"Код скопирован: {product.Id}");
            }
        }

        #endregion
    }
}
public class WriteOffResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public int DocumentId { get; set; }
    public string DocumentNumber { get; set; }
}