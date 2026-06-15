using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.Mdlp;
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
    private readonly IMdlpService _mdlpService;
    private readonly PharmacyWarehouseContext _context;


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

    public int? PreselectedProductId { get; set; }
    public string? RestrictedGtin { get; set; }

    public ReceiptPage(int? documentId = null)
    {
        InitializeComponent();
        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _supplierService = App.ServiceProvider.GetService<SupplierService>();
        _authService = App.ServiceProvider.GetService<AuthService>();
        _mdlpService = App.ServiceProvider.GetService<IMdlpService>();
        _context = new PharmacyWarehouseContext();

        _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";
        dpExpirationDate.DisplayDateStart = DateTime.Now;

        LoadData();

        DataContext = this;
        Loaded += (s, e) =>
        {
            if (documentId.HasValue)
                LoadDraft(documentId.Value);
            else
            {
                InitializeNewDocument();
                ApplyPreselectedProduct();
            }
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

    private async void ApplyPreselectedProduct()
    {
        if (!PreselectedProductId.HasValue) return;

        await Task.Delay(100);
        
        var product = _allProducts.FirstOrDefault(p => p.Id == PreselectedProductId.Value);
        if (product == null)
        {
            product = await _context.Products.FirstOrDefaultAsync(p => p.Id == PreselectedProductId.Value);
            if (product != null)
            {
                _allProducts.Add(product);
                _allProductsObservable.Add(product);
                _filteredProductsView?.Refresh();
            }
        }
        
        if (product != null)
        {
            RestrictedGtin = product.Gtin;
            
            Dispatcher.Invoke(() =>
            {
                if (product.IsTracked)
                {
                    // Для маркированных товаров: фокус на сканер, блокируем ComboBox
                    cmbProduct.IsEnabled = false;
                    txtScanCode.Focus();
                    txtScanError.Text = $"✅ Товар выбран. Пожалуйста, сканируйте DataMatrix.";
                    txtScanError.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    // Для немаркированных: выбираем товар
                    cmbProduct.SelectedItem = product;
                    
                    // Заполняем цену последней закупки
                    var lastPurchasePrice = product.Batches
                        .Where(b => b.IsActive)
                        .OrderByDescending(b => b.ArrivalDate)
                        .Select(b => b.PurchasePrice)
                        .FirstOrDefault();

                    if (lastPurchasePrice > 0)
                    {
                        txtPurchasePrice.Text = lastPurchasePrice.ToString("N2");
                        txtSellingPrice.Text = (lastPurchasePrice * 1.3m).ToString("N2");
                    }
                }
            });
        }
        else
        {
            MessageBox.Show($"Товар с ID {PreselectedProductId.Value} не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        PreselectedProductId = null;
    }

    private void InitializeNewDocument()
    {
        _currentDocument = new Document
        {
            Type = DocumentType.Incoming,
            Date = DateTime.Now,
            Status = DocumentStatus.Draft,
            CreatedBy = _currentUser,
            CreatedAt = DateTime.Now,
            Number = string.Empty // Let CreateIncomingDocument generate it inside the transaction
        };

        txtDocNumber.Text = "(будет сгенерирован при сохранении)";
        dpDocDate.SelectedDate = _currentDocument.Date;
        dpInvoiceDate.SelectedDate = DateTime.Now;

        btnProcessDocument.IsEnabled = true;
        txtAlreadySentHint.Visibility = Visibility.Collapsed;

        _documentItems.Clear();
        UpdateDocumentTotals();
        UpdateSelectedItemInfo();

        ResetItemForm();
        
        // Устанавливаем фокус на поле сканирования
        Dispatcher.BeginInvoke(() => txtScanCode.Focus());
    }

    private async void LoadDraft(int documentId)
    {
        try
        {
            var draft = _documentService.GetById(documentId);
            if (draft == null || draft.Type != DocumentType.Incoming)
                return;

            _currentDocument = draft;

            using var db = new PharmacyWarehouseContext();
            var existingMdlpDoc = await db.MdlpDocuments
                .FirstOrDefaultAsync(d => d.DocumentId == _currentDocument.Id);

            if (existingMdlpDoc != null)
            {
                btnProcessDocument.IsEnabled = false;
                txtAlreadySentHint.Visibility = Visibility.Visible;
                txtAlreadySentHint.Text = $"Документ уже отправлен в МДЛП (статус: {existingMdlpDoc.StatusText})";
            }
            else
            {
                btnProcessDocument.IsEnabled = true;
                txtAlreadySentHint.Visibility = Visibility.Collapsed;
            }

            txtDocNumber.Text = draft.Number;
            dpDocDate.SelectedDate = draft.Date;
            cmbSupplier.SelectedValue = draft.SupplierId;
            txtInvoiceNumber.Text = draft.SupplierInvoiceNumber;
            dpInvoiceDate.SelectedDate = draft.SupplierInvoiceDate;
            txtNotes.Text = draft.Notes;

            _documentItems.Clear();
            foreach (var line in draft.DocumentLines)
            {
                // Find the product for this line
                var product = _allProducts.FirstOrDefault(p => p.Id == line.ProductId);
                line.Product = product;
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

            _filteredProductsView = CollectionViewSource.GetDefaultView(_allProductsObservable);
            _filteredProductsView.Filter = FilterProduct;

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
        if (obj is not Product product)
            return false;
            
        // Показываем только немаркированные товары в ComboBox
        if (product.IsTracked)
            return false;

        if (string.IsNullOrWhiteSpace(ProductSearchText))
            return true;

        var searchText = ProductSearchText.ToLower();
        return product.Name.ToLower().Contains(searchText) ||
               (product.Manufacturer?.ToLower().Contains(searchText) ?? false) ||
               (product.Description?.ToLower().Contains(searchText) ?? false);
    }

    private string GenerateDocumentNumber()
    {
        var date = DateTime.Now;
        var count = _documentService.GetDocumentsCount(date.Month, date.Year, PharmacyWarehouse.Models.DocumentType.Incoming);
        return $"ПР-{date:MMyy}-{count + 1:D4}";
    }

    #endregion

    #region DataMatrix and GTIN Handling

    private void TxtScanCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        ProcessScanCode(txtScanCode.Text.Trim());
    }

    private void ProcessScanCode(string code)
    {
        // Скрываем предыдущую ошибку
        txtScanError.Text = "";

        // Проверка: это только цифры и длина 13?
        if (code.All(char.IsDigit) && code.Length == 13)
        {
            // EAN-13: добавляем ведущий ноль до 14 символов
            string gtin = "0" + code;
            ProcessGtin(gtin);
            return;
        }

        // Иначе - DataMatrix
        ProcessDataMatrix(code);
    }

    private void ProcessDataMatrix(string code)
    {
        var result = DataMatrixParser.Parse(code);
        if (!result.IsValid)
        {
            ShowScanError(result.ErrorMessage ?? "Некорректный DataMatrix код");
            return;
        }

        if (!string.IsNullOrEmpty(result.Sgtin))
        {
            ProcessSgtin(result.Sgtin, result);
        }
        else if (!string.IsNullOrEmpty(result.Gtin))
        {
            ProcessGtin(result.Gtin, result);
        }
        else
        {
            ShowScanError("Код не содержит GTIN или SGTIN");
        }
    }

    private void ProcessSgtin(string sgtin, DataMatrixResult dataMatrixResult)
    {
        string gtinFromSgtin = sgtin.Substring(0, 14);
        
        // Проверка на ограничение по GTIN
        if (!string.IsNullOrEmpty(RestrictedGtin) && gtinFromSgtin != RestrictedGtin)
        {
            ShowScanError($"Пожалуйста, сканируйте только выбранный товар (GTIN: {RestrictedGtin})");
            return;
        }
        
        var product = _allProducts.FirstOrDefault(p => p.Gtin == gtinFromSgtin);
        
        if (product == null)
        {
            ShowScanError($"Товар с GTIN {gtinFromSgtin} не найден");
            return;
        }

        if (!product.IsTracked)
        {
            ShowScanError("Товар не маркированный, используйте GTIN");
            return;
        }

        using var db = new PharmacyWarehouseContext();
        var existingSgtin = db.MdlpSgtins.FirstOrDefault(s => s.Sgtin == sgtin);
        if (existingSgtin != null && existingSgtin.Status != "Withdrawn" && existingSgtin.Status != "WrittenOff")
        {
            ShowScanError("Этот SGTIN уже в обращении");
            return;
        }

        var inCurrentDoc = _documentItems.Any(item => item.Sgtin == sgtin);
        if (inCurrentDoc)
        {
            ShowScanError("Этот SGTIN уже в документе");
            return;
        }

        // Проверка цены закупки - обязательна
        if (!decimal.TryParse(txtPurchasePrice.Text, out decimal purchasePrice) || purchasePrice <= 0)
        {
            ShowScanError("Введите цену закупки перед сканированием");
            return;
        }

        // Получаем цену продажи из поля
        decimal sellingPrice;
        if (!decimal.TryParse(txtSellingPrice.Text, out sellingPrice) || sellingPrice <= 0)
        {
            sellingPrice = purchasePrice * 1.3m;
        }

        try
        {
            var documentLine = new DocumentLine
            {
                ProductId = product.Id,
                Product = product, 
                Quantity = 1,
                UnitPrice = purchasePrice,
                SellingPrice = sellingPrice,
                Notes = $"Поступление от поставщика",
                Sgtin = sgtin,
                Series = dataMatrixResult.Series ?? GenerateRandomSeries(),
                ExpirationDate = dataMatrixResult.ExpirationDate
            };

            _documentItems.Add(documentLine);
            UpdateDocumentTotals();

            ResetScanField();
            txtScanError.Text = $"✅ Товар добавлен: {product.Name}";
            txtScanError.Foreground = System.Windows.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            ShowScanError($"Ошибка: {ex.Message}");
        }
    }

    private void ProcessGtin(string gtin, DataMatrixResult? dataMatrixResult = null)
    {
        // Проверка на ограничение по GTIN
        if (!string.IsNullOrEmpty(RestrictedGtin) && gtin != RestrictedGtin)
        {
            ShowScanError($"Пожалуйста, сканируйте только выбранный товар (GTIN: {RestrictedGtin})");
            return;
        }
        
        var product = _allProducts.FirstOrDefault(p => p.Gtin == gtin);
        if (product == null)
        {
            ShowScanError($"Товар с GTIN {gtin} не найден");
            return;
        }

        if (product.IsTracked)
        {
            ShowScanError("Товар маркированный, нужен полный DataMatrix");
            return;
        }

        // Проверка цены закупки - обязательна
        if (!decimal.TryParse(txtPurchasePrice.Text, out decimal purchasePrice) || purchasePrice <= 0)
        {
            ShowScanError("Введите цену закупки перед сканированием");
            return;
        }

        // Получаем цену продажи из поля
        decimal sellingPrice;
        if (!decimal.TryParse(txtSellingPrice.Text, out sellingPrice) || sellingPrice <= 0)
        {
            var lastPurchasePrice = product.Batches
                .Where(b => b.IsActive)
                .OrderByDescending(b => b.ArrivalDate)
                .Select(b => b.PurchasePrice)
                .FirstOrDefault();
            
            if (lastPurchasePrice > 0)
            {
                sellingPrice = lastPurchasePrice * 1.3m;
            }
            else
            {
                sellingPrice = purchasePrice * 1.3m;
            }
        }
        
        // Получаем количество из поля
        if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
        {
            quantity = 1;
        }

        // Генерируем серию и получаем срок годности
        string series = GenerateRandomSeries();
        DateOnly? expirationDate = null;
        
        if (dataMatrixResult != null)
        {
            if (!string.IsNullOrEmpty(dataMatrixResult.Series))
            {
                series = dataMatrixResult.Series;
            }

            if (dataMatrixResult.ExpirationDate.HasValue)
            {
                expirationDate = dataMatrixResult.ExpirationDate;
            }
        }

        try
        {
            var documentLine = new DocumentLine
            {
                ProductId = product.Id,
                Product = product, 
                Quantity = quantity,
                UnitPrice = purchasePrice,
                SellingPrice = sellingPrice,
                Notes = $"Поступление от поставщика",
                Series = series,
                ExpirationDate = expirationDate
            };

            _documentItems.Add(documentLine);
            UpdateDocumentTotals();

            ResetScanField();
            txtScanError.Text = $"✅ Товар добавлен: {product.Name}";
            txtScanError.Foreground = System.Windows.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            ShowScanError($"Ошибка: {ex.Message}");
        }
    }

    private void ShowScanError(string message)
    {
        txtScanError.Text = $"❌ {message}";
        txtScanError.Foreground = System.Windows.Media.Brushes.Red;
    }

    private string GenerateRandomSeries()
    {
        var random = new Random();
        // Cyrillic letters commonly used in series: С, Т (and maybe others? Let's use С and Т as per examples)
        char[] prefixes = new char[] { 'С', 'Т' };
        char prefix = prefixes[random.Next(prefixes.Length)];
        
        // Get current date in format YYMMDD
        var today = DateTime.Now;
        string datePart = today.ToString("yyMMdd");
        
        return $"{prefix}{datePart}";
    }

    private void ResetScanField()
    {
        txtScanCode.Text = "";
        txtPurchasePrice.Text = "";
        txtSellingPrice.Text = "";
        Dispatcher.BeginInvoke(() => txtScanCode.Focus());
    }

    #endregion

    #region Document Items Management

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProduct.SelectedItem == null)
        {
            ShowError("Ошибка", "Выберите товар");
            return;
        }

        var selectedProduct = cmbProduct.SelectedItem as Product;
        if (selectedProduct == null) return;

        if (selectedProduct.IsTracked)
        {
            ShowError("Ошибка", "Для маркированных используйте блок слева");
            return;
        }

        if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
        {
            ShowError("Ошибка", "Введите корректное количество");
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

        if (!decimal.TryParse(txtPurchasePrice.Text, out decimal price) || price <= 0)
        {
            ShowError("Ошибка", "Введите корректную цену");
            return;
        }

        // Получаем цену продажи
        decimal sellingPrice;
        if (!decimal.TryParse(txtSellingPrice.Text, out sellingPrice) || sellingPrice <= 0)
        {
            sellingPrice = price * 1.3m;
        }

        try
        {
            var documentLine = new DocumentLine
            {
                ProductId = selectedProduct.Id,
                Product = selectedProduct, 
                Quantity = quantity,
                UnitPrice = price,
                SellingPrice = sellingPrice, 
                Series = txtSeries.Text.Trim(),
                ExpirationDate = DateOnly.FromDateTime(dpExpirationDate.SelectedDate.Value),
                Notes = $"Поступление от поставщика"
            };

            _documentItems.Add(documentLine);

            ResetItemForm();
            UpdateDocumentTotals();
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
        txtSellingPrice.Text = selectedDocumentLine.SellingPrice.Value.ToString("N2");
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
            // txtLineTotal removed in new design
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
    }

    private void ResetItemForm()
    {
        txtQuantity.Text = "1";
        txtPurchasePrice.Text = "";
        txtSellingPrice.Text = "";
        txtSeries.Text = "";
        dpExpirationDate.SelectedDate = null;
        cmbProduct.SelectedItem = null;
        txtScanCode.Text = "";
        txtScanError.Text = "";
    }

    #endregion

    #region Document Operations

    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
            _currentDocument.SupplierId = cmbSupplier.SelectedValue as int?;
            _currentDocument.SupplierInvoiceNumber = txtInvoiceNumber.Text;
            _currentDocument.SupplierInvoiceDate = dpInvoiceDate.SelectedDate;
            _currentDocument.Notes = txtNotes.Text;
            _currentDocument.Status = DocumentStatus.Draft;
            _currentDocument.Amount = _documentTotal;

            var documentLines = new List<DocumentLine>();
            foreach (var item in _documentItems)
            {
                var line = new DocumentLine
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    SellingPrice = item.SellingPrice,
                    Series = item.Series,
                    ExpirationDate = item.ExpirationDate,
                    Notes = $"Поступление от поставщика",
                    Sgtin = item.Sgtin
                };

                if (item.Id != 0)
                {
                    line.Id = item.Id;
                }

                documentLines.Add(line);
            }

            int documentId = _documentService.CreateIncomingDocument(_currentDocument, documentLines);
            _currentDocument.Id = documentId;

            ShowInfo($"Черновик сохранен. Номер документа: {_currentDocument.Number}");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка сохранения", ex.Message);
        }
    }

    private async void ProcessDocument_Click(object sender, RoutedEventArgs e)
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
                        SellingPrice = item.SellingPrice,
                        Series = item.Series,
                        ExpirationDate = item.ExpirationDate,
                        Notes = item.Notes,
                        Sgtin = item.Sgtin
                    };
                    linesForDb.Add(line);
                }

                int documentId;
                if (_currentDocument.Id == 0)
                {
                    documentId = _documentService.CreateIncomingDocument(_currentDocument, linesForDb);
                    _currentDocument.Id = documentId;
                    txtDocNumber.Text = _currentDocument.Number; // Update UI with real number
                }
                else
                {
                    _documentService.UpdateDocument(_currentDocument, linesForDb);
                    documentId = _currentDocument.Id;
                }

                // Now we need a fresh context for Mdlp checks
                using var freshContext = new PharmacyWarehouseContext();
                var existingMdlpDoc = await freshContext.MdlpDocuments
                    .FirstOrDefaultAsync(d => d.DocumentId == _currentDocument.Id);

                if (existingMdlpDoc != null)
                {
                    MessageBox.Show($"Документ уже отправлен в МДЛП.\nСтатус: {existingMdlpDoc.StatusText}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NavigateToDocumentsPage();
                    return;
                }

                var mdlpResult = await _mdlpService.SendReceiptAsync(_currentDocument);
                string mdlpStatusText = mdlpResult.Status == "Accepted" ? "Принято" : "Ожидает обработки";

                MessageBox.Show($"Документ проведен успешно!\nНомер: {_currentDocument.Number}\nID: {documentId}\nМДЛП: {mdlpStatusText} (ID: {mdlpResult.MdlpDocumentId})",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                
                NavigateToDocumentsPage();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка проведения документа", ex.Message);
            }
        }
    }

    private void NavigateToDocumentsPage()
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow != null)
        {
            var documentsPage = App.ServiceProvider.GetService<DocumentsPage>();
            mainWindow.MainFrame.Navigate(documentsPage);
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
            var lastPurchasePrice = product.Batches
                .Where(b => b.IsActive)
                .OrderByDescending(b => b.ArrivalDate)
                .Select(b => b.PurchasePrice)
                .FirstOrDefault();

            if (lastPurchasePrice > 0)
            {
                txtPurchasePrice.Text = lastPurchasePrice.ToString("N2");
                txtSellingPrice.Text = (lastPurchasePrice * 1.3m).ToString("N2");
            }
        }
    }

    private void TxtPurchasePrice_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (decimal.TryParse(txtPurchasePrice.Text, out decimal purchasePrice) && purchasePrice > 0)
        {
            txtSellingPrice.Text = (purchasePrice * 1.3m).ToString("N2");
        }
    }

    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void DecimalValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        var textBox = sender as TextBox;
        var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
        e.Handled = !decimal.TryParse(newText, out _);
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

    private void txtScanCode_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void txtSeries_TextChanged(object sender, TextChangedEventArgs e)
    {

    }
}
