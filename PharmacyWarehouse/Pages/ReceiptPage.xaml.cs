using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PharmacyWarehouse.Pages;

public partial class ReceiptPage : Page, INotifyPropertyChanged
{
    private readonly DocumentService _documentService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService; 
    private readonly AuthService _authService;


    private Document _currentDocument;
    private ObservableCollection<DocumentLine> _documentItems;
    private List<Product> _allProducts = new();
    private List<Supplier> _allSuppliers = new();

    private ICollectionView _filteredProductsView;
    private string _productSearchText = string.Empty;
    private ObservableCollection<Product> _allProductsObservable = new();

    public event PropertyChangedEventHandler PropertyChanged;

    private bool _isPageLoaded = false;
    private decimal _documentTotal = 0m;
    private int _totalQuantity = 0;
    private string _currentUser = String.Empty;


    public ReceiptPage(int? documentId = null)
    {
        InitializeComponent();
        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _supplierService = App.ServiceProvider.GetService<SupplierService>();
        _authService = App.ServiceProvider.GetService<AuthService>();

        _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";

        LoadData();

        DataContext = this;
        Loaded += (s, e) =>
        {
            if (documentId.HasValue)
                LoadDraft(documentId.Value);
            else
                InitializeNewDocument();
        };

        _documentItems = new ObservableCollection<DocumentLine>();
        dgItems.ItemsSource = _documentItems;
    }

    #region Properties for Binding

    public ICollectionView FilteredProducts => _filteredProductsView;

    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            _productSearchText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductSearchText)));
            if (_filteredProductsView != null)
            {
                _filteredProductsView.Refresh();
            }
        }
    }

    #endregion

    #region Initialization

    private void InitializeNewDocument()
    {
        _currentDocument = new Document
        {
            Type = DocumentType.Incoming,
            Date = DateTime.Now,
            Status = DocumentStatus.Draft,
            CreatedBy = _currentUser,
            CreatedAt = DateTime.Now,
            Number = GenerateDocumentNumber()
        };

        txtDocNumber.Text = _currentDocument.Number;
        dpDocDate.SelectedDate = _currentDocument.Date;
        dpInvoiceDate.SelectedDate = DateTime.Now;

        _documentItems.Clear();
        UpdateDocumentTotals();
        UpdateSelectedItemInfo();
    }

    private void LoadDraft(int documentId)
    {
        try
        {
            var draft = _documentService.GetById(documentId);
            if (draft == null || draft.Type != DocumentType.Incoming)
                return;

            _currentDocument = draft;

            // Заполняем поля формы
            txtDocNumber.Text = draft.Number;
            dpDocDate.SelectedDate = draft.Date;
            cmbSupplier.SelectedValue = draft.SupplierId;
            txtInvoiceNumber.Text = draft.SupplierInvoiceNumber;
            dpInvoiceDate.SelectedDate = draft.SupplierInvoiceDate;
            txtNotes.Text = draft.Notes;

            // Загружаем позиции
            _documentItems.Clear();
            foreach (var line in draft.DocumentLines)
            {
                _documentItems.Add(line);
            }

            UpdateDocumentTotals();
            ShowInfo("Черновик загружен");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки черновика", ex.Message);
            InitializeNewDocument();
        }
    }

    private async void LoadData()
    {
        try
        {
            // Загружаем поставщиков
            _allSuppliers.Clear();
            var suppliers = _supplierService.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            foreach (var supplier in suppliers)
            {
                _allSuppliers.Add(supplier);
            }

            cmbSupplier.ItemsSource = _allSuppliers;

            // Загружаем товары
            _allProducts.Clear();
            _allProductsObservable.Clear();

            var products = _productService.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList();

            foreach (var product in products)
            {
                _allProducts.Add(product);
                _allProductsObservable.Add(product);
            }

            // Настраиваем фильтрацию товаров
            _filteredProductsView = CollectionViewSource.GetDefaultView(_allProductsObservable);
            _filteredProductsView.Filter = FilterProduct;

            // Привязываем к ComboBox
            cmbProduct.SetBinding(ComboBox.ItemsSourceProperty,
                new Binding("FilteredProducts") { Source = this });
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки данных", ex.Message);
        }
    }

    private bool FilterProduct(object obj)
    {
        if (obj is not Product product || string.IsNullOrWhiteSpace(ProductSearchText))
            return true;

        var searchText = ProductSearchText.ToLower();
        return product.Name.ToLower().Contains(searchText) ||
               (product.Manufacturer?.ToLower().Contains(searchText) ?? false) ||
               (product.Description?.ToLower().Contains(searchText) ?? false);
    }

    private string GenerateDocumentNumber()
    {
        var date = DateTime.Now;
        var count = _documentService.GetDocumentsCount(date.Month, date.Year);
        return $"ПР-{date:MMyy}-{count + 1:D4}";
    }

    #endregion

    #region Document Items Management

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        // Валидация
        if (cmbProduct.SelectedItem == null)
        {
            ShowError("Ошибка", "Выберите товар");
            return;
        }

        if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
        {
            ShowError("Ошибка", "Введите корректное количество");
            return;
        }

        if (!decimal.TryParse(txtPurchasePrice.Text, out decimal price) || price <= 0)
        {
            ShowError("Ошибка", "Введите корректную цену");
            return;
        }

        if (string.IsNullOrWhiteSpace(txtSeries.Text))
        {
            ShowError("Ошибка", "Введите серию товара");
            return;
        }

        if (!dpExpirationDate.SelectedDate.HasValue)
        {
            ShowError("Ошибка", "Выберите срок годности");
            return;
        }

        var selectedProduct = cmbProduct.SelectedItem as Product;
        if (selectedProduct == null) return;

        try
        {
            var documentLine = new DocumentLine
            {
                ProductId = selectedProduct.Id,
                Product = selectedProduct, 
                Quantity = quantity,
                UnitPrice = price,
                SellingPrice = price * 1.3m, 
                Series = txtSeries.Text.Trim(),
                ExpirationDate = DateOnly.FromDateTime(dpExpirationDate.SelectedDate.Value),
                Notes = $"Поступление от поставщика"
            };

            _documentItems.Add(documentLine);

            txtQuantity.Text = "1";
            txtPurchasePrice.Text = "0.00";
            txtSeries.Text = "";
            dpExpirationDate.SelectedDate = null;
            cmbProduct.SelectedItem = null;

            UpdateDocumentTotals();
            UpdateLineTotal();
            ShowInfo($"Товар '{selectedProduct.Name}' добавлен");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка добавления", ex.Message);
        }
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (dgItems.SelectedItem is not DocumentLine selectedDocumentLine)
        {
            ShowInfo("Выберите позицию для редактирования");
            return;
        }

        var product = selectedDocumentLine.Product;
        if (product != null)
        {
            cmbProduct.SelectedItem = _allProducts.FirstOrDefault(p => p.Id == product.Id);
        }

        txtQuantity.Text = selectedDocumentLine.Quantity.ToString();
        txtPurchasePrice.Text = selectedDocumentLine.UnitPrice.ToString("N2");
        txtSeries.Text = selectedDocumentLine.Series ?? "";

        if (selectedDocumentLine.ExpirationDate.HasValue)
        {
            dpExpirationDate.SelectedDate = selectedDocumentLine.ExpirationDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        _documentItems.Remove(selectedDocumentLine);
        UpdateDocumentTotals();

        ShowInfo("Позиция готова к редактированию");
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DocumentLine documentLine)
        {
            var productName = documentLine.Product?.Name ?? "товара";

            var result = MessageBox.Show(
                $"Удалить позицию '{productName}'?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _documentItems.Remove(documentLine);
                UpdateDocumentTotals();
                ShowInfo("Позиция удалена");
            }
        }
    }

    private void UpdateDocumentTotals()
    {
        _totalQuantity = _documentItems.Sum(item => item.Quantity);
        _documentTotal = _documentItems.Sum(item => item.TotalPrice);

        txtItemsCount.Text = _documentItems.Count.ToString();
        txtTotalQuantity.Text = _totalQuantity.ToString("N0");
        txtTotalAmount.Text = _documentTotal.ToString("N2");
    }

    private void UpdateLineTotal()
    {
        if (int.TryParse(txtQuantity.Text, out int quantity) &&
            decimal.TryParse(txtPurchasePrice.Text, out decimal price))
        {
            txtLineTotal.Text = (quantity * price).ToString("N2");
        }
        else
        {
            txtLineTotal.Text = "0.00";
        }
    }

    private void UpdateSelectedItemInfo()
    {
        if (dgItems.SelectedItem is DocumentLine selectedLine && selectedLine.Product != null)
        {
            txtSelectedProduct.Text = selectedLine.Product.Name;
            txtProductCategory.Text = selectedLine.Product.Category?.Name ?? "Без категории";
            txtProductManufacturer.Text = selectedLine.Product.Manufacturer;
            txtCurrentStock.Text = selectedLine.Product.CurrentStock.ToString("N0");
            txtMinRemainder.Text = selectedLine.Product.MinRemainder.ToString("N0");
        }
        else
        {
            txtSelectedProduct.Text = "Не выбран товар";
            txtProductCategory.Text = "-";
            txtProductManufacturer.Text = "-";
            txtCurrentStock.Text = "0";
            txtMinRemainder.Text = "0";
        }

        txtSelectedItemsInfo.Text = $"Выбрано: {dgItems.SelectedItems.Count}";
    }
    #endregion

    #region Document Operations

    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Обновляем данные документа
            _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
            _currentDocument.SupplierId = cmbSupplier.SelectedValue as int?;
            _currentDocument.SupplierInvoiceNumber = txtInvoiceNumber.Text;
            _currentDocument.SupplierInvoiceDate = dpInvoiceDate.SelectedDate;
            _currentDocument.Notes = txtNotes.Text;
            _currentDocument.Status = DocumentStatus.Draft;
            _currentDocument.Amount = _documentTotal;

            // Создаем список строк документа
            var documentLines = new List<DocumentLine>();
            foreach (var item in _documentItems)
            {
                var line = new DocumentLine
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    SellingPrice = item.SellingPrice * 1.3m,
                    Series = item.Series,
                    ExpirationDate = item.ExpirationDate,
                    Notes = $"Поступление от поставщика"
                };

                // Если это редактирование существующей строки, сохраняем ID
                if (item.Id != 0)
                {
                    line.Id = item.Id;
                }

                documentLines.Add(line);
            }

            // Сохраняем документ
            int documentId = _documentService.CreateIncomingDocument(_currentDocument, documentLines);

            // Обновляем ID текущего документа
            _currentDocument.Id = documentId;

            ShowInfo($"Черновик сохранен. Номер документа: {_currentDocument.Number}");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка сохранения", ex.Message);
        }
    }

    private void ProcessDocument_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateDocument())
            return;

        if (_documentItems.Count == 0)
        {
            ShowError("Ошибка", "Добавьте хотя бы одну позицию в документ");
            return;
        }

        var result = MessageBox.Show(
            $"Провести приходную накладную №{_currentDocument.Number}?\n\n" +
            $"Поставщик: {cmbSupplier.Text}\n" +
            $"Количество позиций: {_documentItems.Count}\n" +
            $"Общая сумма: {_documentTotal:N2} руб.",
            "Проведение документа",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Обновляем данные документа
                _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
                _currentDocument.SupplierId = cmbSupplier.SelectedValue as int?;
                _currentDocument.SupplierInvoiceNumber = txtInvoiceNumber.Text;
                _currentDocument.SupplierInvoiceDate = dpInvoiceDate.SelectedDate;
                _currentDocument.Notes = txtNotes.Text;
                _currentDocument.Status = DocumentStatus.Processed;
                _currentDocument.Amount = _documentTotal;
                _currentDocument.SignedBy = _currentUser;
                _currentDocument.SignedAt = DateTime.Now;

                var linesForDb = new List<DocumentLine>();
                foreach (var item in _documentItems)
                {
                    var line = new DocumentLine
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        SellingPrice = item.SellingPrice * 1.3m,
                        Series = item.Series,
                        ExpirationDate = item.ExpirationDate,
                        Notes = item.Notes
                    };
                    linesForDb.Add(line);
                }

                int documentId;
                if (_currentDocument.Id == 0) // Новый документ
                {
                    documentId = _documentService.CreateIncomingDocument(_currentDocument, linesForDb);
                }
                else // Редактирование существующего
                {
                    _documentService.UpdateDocument(_currentDocument, linesForDb);
                    documentId = _currentDocument.Id;
                }

                ShowInfo($"Документ проведен успешно!\nНомер: {_currentDocument.Number}\nID: {documentId}");
                InitializeNewDocument();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка проведения документа", ex.Message);
            }
        }
    }

    private bool ValidateDocument()
    {
        if (cmbSupplier.SelectedItem == null)
        {
            ShowError("Ошибка", "Выберите поставщика");
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtInvoiceNumber.Text))
        {
            ShowError("Ошибка", "Введите номер счета поставщика");
            return false;
        }

        if (!dpInvoiceDate.SelectedDate.HasValue)
        {
            ShowError("Ошибка", "Выберите дату счета поставщика");
            return false;
        }

        string cleanAccount = txtInvoiceNumber.Text.Replace(" ", "").Replace("-", "");

        if (cleanAccount.Length != 20 && !cleanAccount.All(char.IsDigit))
        {
            ShowError("Ошибка", "Введите корректный номер счета поставщика");
            return false;
        }


        return true;
    }

    private void CancelDocument_Click(object sender, RoutedEventArgs e)
    {
        if (_documentItems.Count > 0)
        {
            var result = MessageBox.Show(
                "Отменить создание документа?\nВсе несохраненные данные будут потеряны.",
                "Отмена документа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        InitializeNewDocument();
        ShowInfo("Документ отменен");
    }

    #endregion

    #region UI Event Handlers

    private void DgItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedItemInfo();
    }

    private void CmbSupplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPageLoaded && cmbSupplier.SelectedItem is Supplier supplier)
        {
            ShowInfo($"Выбран поставщик: {supplier.Name}");
        }
    }

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPageLoaded && cmbProduct.SelectedItem is Product product)
        {
            // Устанавливаем рекомендуемую цену закупки
            var lastPurchasePrice = product.Batches
                .Where(b => b.IsActive)
                .OrderByDescending(b => b.ArrivalDate)
                .Select(b => b.PurchasePrice)
                .FirstOrDefault();

            if (lastPurchasePrice > 0)
            {
                txtPurchasePrice.Text = lastPurchasePrice.ToString("N2");
            }

            UpdateLineTotal();
        }
    }

    // Валидация числового ввода
    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
        UpdateLineTotal();
    }

    private void DecimalValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        var textBox = sender as TextBox;
        var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

        e.Handled = !decimal.TryParse(newText, out _);

        if (!e.Handled)
            UpdateLineTotal();
    }

    private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPageLoaded)
            UpdateLineTotal();
    }

    private void TxtPurchasePrice_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPageLoaded)
            UpdateLineTotal();
    }

    #endregion

    #region Additional Features

    private void CopyNumber_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_currentDocument.Number);
        ShowInfo($"Номер документа скопирован: {_currentDocument.Number}");
    }

    private void ShowStatistics_Click(object sender, RoutedEventArgs e)
    {
        var stats = new StringBuilder();
        stats.AppendLine($"📊 Статистика документа:");
        stats.AppendLine($"• Поставщик: {cmbSupplier.Text ?? "Не выбран"}");
        stats.AppendLine($"• Позиций: {_documentItems.Count}");
        stats.AppendLine($"• Общее количество: {_totalQuantity}");
        stats.AppendLine($"• Общая сумма: {_documentTotal:N2} руб.");
        stats.AppendLine($"• Средняя цена: {(_documentItems.Count > 0 ? _documentTotal / _totalQuantity : 0):N2} руб.");

        MessageBox.Show(stats.ToString(), "Статистика документа",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PrintDocument_Click(object sender, RoutedEventArgs e)
    {
        if (_documentItems.Count == 0)
        {
            ShowError("Ошибка", "Документ пуст");
            return;
        }

        ShowInfo("Функция печати будет реализована позже");
    }

    private void AddAnotherItem_Click(object sender, RoutedEventArgs e)
    {
        cmbProduct.Focus();
    }

    private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
    {
        ShowInfo("Функция импорта из Excel будет реализована позже");
    }

    private void CalculatePreview_Click(object sender, RoutedEventArgs e)
    {
        var preview = new StringBuilder();
        preview.AppendLine("📊 Предварительный расчет:");
        preview.AppendLine($"• Количество позиций: {_documentItems.Count}");
        preview.AppendLine($"• Общая сумма: {_documentTotal:N2} руб.");

        if (cmbSupplier.SelectedItem is Supplier supplier)
        {
            preview.AppendLine($"• Поставщик: {supplier.Name}");
            preview.AppendLine($"• Контакт: {supplier.ContactPerson}");
        }

        MessageBox.Show(preview.ToString(), "Предварительный расчет",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyItem_Click(object sender, RoutedEventArgs e)
    {
        if (dgItems.SelectedItem is DocumentLine documentLine)
        {
            var product = documentLine.Product;

            Clipboard.SetText($"{product?.Name ?? "Неизвестный товар"} - {documentLine.Quantity} шт. - {documentLine.UnitPrice:N2} руб.");
            ShowInfo("Позиция скопирована в буфер обмена");
        }
    }

    private void ShowProductInfo_Click(object sender, RoutedEventArgs e)
    {
        if (dgItems.SelectedItem is DocumentLine documentLine && documentLine.Product != null)
        {
            var product = documentLine.Product;
            var info = new StringBuilder();
            info.AppendLine($"ℹ️ Информация о товаре:");
            info.AppendLine($"• Название: {product.Name}");
            info.AppendLine($"• Производитель: {product.Manufacturer}");
            info.AppendLine($"• Категория: {product.Category?.Name ?? "Без категории"}");
            info.AppendLine($"• Форма выпуска: {product.ReleaseForm}");
            info.AppendLine($"• Ед. измерения: {product.UnitOfMeasure}");
            info.AppendLine($"• Текущий остаток: {product.CurrentStock} шт.");
            info.AppendLine($"• Минимальный остаток: {product.MinRemainder} шт.");
            info.AppendLine($"• Рецептурный: {(product.RequiresPrescription ? "Да" : "Нет")}");

            MessageBox.Show(info.ToString(), "Информация о товаре",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ShowDeliveryHistory_Click(object sender, RoutedEventArgs e)
    {
        if (dgItems.SelectedItem is DocumentLine documentLine && documentLine.Product != null)
        {
            var product = documentLine.Product;
            ShowInfo($"История поставок для товара: {product.Name}");
        }
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
