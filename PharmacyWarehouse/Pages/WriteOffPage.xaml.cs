using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.Mdlp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PharmacyWarehouse.Pages;

public partial class WriteOffPage : Page, INotifyPropertyChanged
{
    private readonly DocumentService _documentService;
    private readonly ProductService _productService;
    private readonly BatchService _batchService;
    private readonly AuthService _authService;
    private readonly IMdlpService _mdlpService;


    private Document _currentDocument;
    private ObservableCollection<DocumentLine> _documentItems;
    private List<Product> _allProducts = new();
    private List<Product> _productsWithStock = new();
    private List<Batch> _availableBatches = new();
    private Batch _selectedBatch;

    private ICollectionView _filteredProductsView;
    private string _productSearchText = string.Empty;
    private ObservableCollection<Product> _allProductsObservable = new();

    public event PropertyChangedEventHandler PropertyChanged;

    private decimal _documentTotal = 0m;
    private int _totalQuantity = 0;
    private string _currentUser = String.Empty;

    public int? PreselectedProductId { get; set; }
    public string? RestrictedGtin { get; set; }

    public WriteOffPage(int? documentId = null, Batch? selectedBatch = null)
    {
        InitializeComponent();
        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _batchService = App.ServiceProvider.GetService<BatchService>();
        _authService = App.ServiceProvider.GetService<AuthService>();
        _mdlpService = App.ServiceProvider.GetService<IMdlpService>();

        _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";

        LoadData();

        DataContext = this;
        Loaded += async (s, e) =>
        {
            // Дожидаемся полной загрузки данных
            await System.Threading.Tasks.Task.Delay(100);
            
            if (selectedBatch != null)
                PrefillWithBatch(selectedBatch);
            else if (documentId.HasValue)
                LoadDraft(documentId.Value);
            else
            {
                InitializeNewDocument();
                // Используем Dispatcher для применения предвыбранного товара после полной инициализации
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyPreselectedProduct();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        };

        _documentItems = new ObservableCollection<DocumentLine>();
        dgItems.ItemsSource = _documentItems;

        // Заполняем причины списания
        InitializeReasons();
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
            Type = DocumentType.WriteOff,
            Date = DateTime.Now,
            Status = DocumentStatus.Draft,
            CreatedBy = _currentUser,
            CreatedAt = DateTime.Now,
            Number = string.Empty
        };

        txtDocNumber.Text = "(будет сгенерирован при сохранении)";
        dpWriteOffDate.SelectedDate = _currentDocument.Date;
        txtResponsiblePerson.Text = _currentUser;
        txtCommission.Text = $"Состав комиссии: {_currentUser}";

        btnProcessWriteOff.IsEnabled = true;
        txtAlreadySentHint.Visibility = Visibility.Collapsed;

        _documentItems.Clear();
        UpdateDocumentTotals();
        ResetScanField();
    }

    private async void ApplyPreselectedProduct()
    {
        if (!PreselectedProductId.HasValue) return;

        // Ждем загрузки данных
        await Task.Delay(100);
        
        // Пытаемся найти товар сначала в списке товаров с остатком, затем в общем списке
        var product = _productsWithStock.FirstOrDefault(p => p.Id == PreselectedProductId.Value);
        if (product == null)
        {
            product = _allProducts.FirstOrDefault(p => p.Id == PreselectedProductId.Value);
        }
        
        if (product == null)
        {
            // Если не найдено, попробуем загрузить из БД
            using var db1 = new PharmacyWarehouseContext();
            product = await db1.Products.FirstOrDefaultAsync(p => p.Id == PreselectedProductId.Value);
            if (product != null)
            {
                // Проверим, есть ли у него остаток
                var hasStock = _batchService.GetByProduct(product.Id)
                    .Any(b => b.IsActive && b.Quantity > 0);
                        
                // Добавим в списки
                _allProducts.Add(product);
                _allProductsObservable.Add(product);
                
                if (hasStock)
                {
                    _productsWithStock.Add(product);
                }
                
                _filteredProductsView?.Refresh();
            }
        }

        if (product != null)
        {
            RestrictedGtin = product.Gtin;
            
            Dispatcher.Invoke(() =>
            {
                cmbProduct.SelectedItem = product;
                // После выбора товара автоматически выбираем первую доступную партию (для немаркированных)
                // или загружаем SGTIN (для маркированных)
                if (product.IsTracked)
                {
                    // Для маркированных: фокус на сканер
                    cmbProduct.IsEnabled = false;
                    if (cmbSgtin.ItemsSource != null && cmbSgtin.Items.Count > 0)
                    {
                        cmbSgtin.SelectedIndex = 0;
                    }
                    Dispatcher.BeginInvoke(() => txtScanCode.Focus());
                }
                else
                {
                    LoadAvailableBatches(product.Id);
                }
            });
        }
        else
        {
            MessageBox.Show($"Товар с ID {PreselectedProductId.Value} не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        PreselectedProductId = null;
    }

    private void PrefillWithBatch(Batch batch)
    {
        try
        {
            InitializeNewDocument();

            var product = _allProducts.FirstOrDefault(p => p.Id == batch.ProductId);
            if (product != null)
            {
                cmbProduct.SelectedItem = product;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (cmbBatchSeries.ItemsSource != null)
                    {
                        var batches = cmbBatchSeries.ItemsSource as List<Batch>;
                        if (batches != null)
                        {
                            var selectedBatchInList = batches.FirstOrDefault(b => b.Id == batch.Id);
                            if (selectedBatchInList != null)
                            {
                                cmbBatchSeries.SelectedItem = selectedBatchInList;

                                txtQuantity.Text = batch.Quantity.ToString();

                                ShowInfo($"Автоматически заполнена форма для списания партии: {batch.Series}");
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            ShowError("Ошибка предзаполнения формы", ex.Message);
        }
    }

    private async void LoadDraft(int documentId)
    {
        try
        {
            var draft = _documentService.GetById(documentId);
            if (draft == null || draft.Type != DocumentType.WriteOff)
                return;

            _currentDocument = draft;

            // Проверяем, был ли документ уже отправлен в МДЛП
            using var db2 = new PharmacyWarehouseContext();
            var existingMdlpDoc = await db2.MdlpDocuments
                .FirstOrDefaultAsync(d => d.DocumentId == _currentDocument.Id);

            if (existingMdlpDoc != null)
            {
                btnProcessWriteOff.IsEnabled = false;
                txtAlreadySentHint.Visibility = Visibility.Visible;
                txtAlreadySentHint.Text = $"Документ уже отправлен в МДЛП (статус: {existingMdlpDoc.StatusText})";
            }
            else
            {
                btnProcessWriteOff.IsEnabled = true;
                txtAlreadySentHint.Visibility = Visibility.Collapsed;
            }

            // Заполняем поля формы
            txtDocNumber.Text = draft.Number;
            dpWriteOffDate.SelectedDate = draft.Date;
            txtResponsiblePerson.Text = draft.CustomerName;
            txtNotes.Text = draft.Notes;

            // Устанавливаем причину списания
            if (!string.IsNullOrEmpty(draft.Notes))
            {
                foreach (ComboBoxItem item in cmbReason.Items)
                {
                    if (item.Content.ToString().Contains(draft.Notes))
                    {
                        cmbReason.SelectedItem = item;
                        break;
                    }
                }
            }

            // Загружаем позиции
            _documentItems.Clear();
            foreach (var line in draft.DocumentLines)
            {
                var product = _allProductsObservable.FirstOrDefault(p => p.Id == line.ProductId);
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
            _allProducts.Clear();
            _allProductsObservable.Clear();

            // Получаем все активные товары
            var products = _productService.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList();

            foreach (var product in products)
            {
                // Добавляем товар в оба списка
                _allProducts.Add(product);
                _allProductsObservable.Add(product);

                var hasStock = _batchService.GetByProduct(product.Id)
                .Any(b => b.IsActive && b.Quantity > 0);

                if (hasStock)
                    _productsWithStock.Add(product);
            }

            // Настраиваем фильтрацию товаров
            _filteredProductsView = CollectionViewSource.GetDefaultView(_allProductsObservable);
            _filteredProductsView.Filter = FilterProduct;

            // Привязываем к ComboBox
            cmbProduct.ItemsSource = _productsWithStock;
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки данных", ex.Message);
        }
    }

    private void InitializeReasons()
    {
        cmbReason.Items.Clear();
        cmbReason.Items.Add(new ComboBoxItem { Content = "Просроченный срок годности", Tag = WriteOffReason.Expired });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Бой/порча товара", Tag = WriteOffReason.Damaged });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Естественная убыль", Tag = WriteOffReason.Other });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Контрольная закупка", Tag = WriteOffReason.Other });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Другая причина", Tag = WriteOffReason.Other });
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
        var count = _documentService.GetDocumentsCount(date.Month, date.Year, PharmacyWarehouse.Models.DocumentType.WriteOff);
        return $"АС-{date:MMyy}-{count + 1:D4}";
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

        var selectedProduct = cmbProduct.SelectedItem as Product;
        if (selectedProduct == null) return;

        if (selectedProduct.IsTracked)
        {
            // Валидация для маркированных товаров
            string sgtin = null;
            if (cmbSgtin.SelectedItem is MdlpSgtin selectedSgtin)
            {
                sgtin = selectedSgtin.Sgtin;
            }
            else if (!string.IsNullOrWhiteSpace(txtSgtinManual.Text))
            {
                sgtin = txtSgtinManual.Text.Trim();
                // Проверяем, что такой SGTIN существует и имеет нужный статус
                using var db3 = new PharmacyWarehouseContext();
                var existingSgtinDb = db3.MdlpSgtins.FirstOrDefault(s => s.Sgtin == sgtin && s.Status == "InCirculation" && s.ProductId == selectedProduct.Id);
                if (existingSgtinDb == null)
                {
                    ShowError("Ошибка", "Код маркировки не найден, неактивен или принадлежит другому товару");
                    return;
                }
            }
            else
            {
                ShowError("Ошибка", "Выберите или введите код маркировки (SGTIN)");
                return;
            }

            // Проверяем, не использован ли уже этот SGTIN в документе
            var existingInDoc = _documentItems.Any(item => item.Sgtin == sgtin);
            if (existingInDoc)
            {
                ShowError("Ошибка", "Этот код маркировки уже добавлен в документ");
                return;
            }

            try
            {
                // Находим партию для этого SGTIN
                using var db4 = new PharmacyWarehouseContext();
                var sgtinObj = db4.MdlpSgtins
                    .Include(s => s.Batch)
                    .FirstOrDefault(s => s.Sgtin == sgtin);
                if (sgtinObj == null)
                {
                    ShowError("Ошибка", "SGTIN не найден в базе данных");
                    return;
                }
                // Load batch
                var batchId = sgtinObj.BatchId; // Now int?
                Batch? selectedBatch = sgtinObj.Batch;
                sgtinObj.Batch = selectedBatch;
                if (selectedBatch == null)
                {
                    ShowError("Ошибка", "Партия для SGTIN не найдена");
                    return;
                }

                var documentLine = new DocumentLine
                {
                    ProductId = selectedProduct.Id,
                    Product = selectedProduct,
                    Quantity = 1,
                    UnitPrice = selectedBatch.SellingPrice,
                    SellingPrice = selectedBatch.SellingPrice,
                    Series = selectedBatch.Series,
                    ExpirationDate = selectedBatch.ExpirationDate,
                    SourceBatchId = selectedBatch.Id,
                    Notes = $"Списание",
                    Sgtin = sgtin
                };

                _documentItems.Add(documentLine);

                // Если партия просрочена, автоматически выбираем причину списания
                if (selectedBatch.IsExpired)
                {
                    AutoSelectExpiredReason();
                }

                // Сбрасываем форму и возвращаем фокус на сканер
                ResetScanField();

                UpdateDocumentTotals();
                ShowInfo($"Товар '{selectedProduct.Name}' добавлен в акт списания с SGTIN: {sgtin}");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка добавления", ex.Message);
            }
        }
        else
        {
            // Для немаркированных товаров
            if (cmbBatchSeries.SelectedItem == null)
            {
                ShowError("Ошибка", "Выберите партию для списания");
                return;
            }

            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                ShowError("Ошибка", "Введите корректное количество");
                return;
            }

            var selectedBatch = cmbBatchSeries.SelectedItem as Batch;
            if (selectedBatch == null) return;

            // Проверка доступного количества
            if (quantity > selectedBatch.Quantity)
            {
                ShowError("Ошибка",
                    $"Недостаточно товара в выбранной партии.\n" +
                    $"Доступно: {selectedBatch.Quantity} шт.\n" +
                    $"Пытаетесь списать: {quantity} шт.");
                return;
            }

            try
            {
                // Пытаемся найти существующую строку с тем же товаром и партией (немаркированным) и объединяем
                var existingLine = _documentItems
                    .FirstOrDefault(item => item.ProductId == selectedProduct.Id &&
                                           string.IsNullOrEmpty(item.Sgtin) &&
                                           item.SourceBatchId == selectedBatch.Id);

                if (existingLine != null)
                {
                    // Объединяем: увеличиваем количество
                    existingLine.Quantity += quantity;
                    ShowInfo($"Количество товара '{selectedProduct.Name}' увеличено до {existingLine.Quantity} шт.");
                }
                else
                {
                    var documentLine = new DocumentLine
                    {
                        ProductId = selectedProduct.Id,
                        Product = selectedProduct,
                        Quantity = quantity,
                        UnitPrice = selectedBatch.SellingPrice,
                        SellingPrice = selectedBatch.SellingPrice,
                        Series = selectedBatch.Series,
                        ExpirationDate = selectedBatch.ExpirationDate,
                        SourceBatchId = selectedBatch.Id,
                        Notes = $"Списание",
                    };

                    _documentItems.Add(documentLine);
                    ShowInfo($"Товар '{selectedProduct.Name}' добавлен в акт списания");
                }

                // Если партия просрочена, автоматически выбираем причину списания
                if (selectedBatch.IsExpired)
                {
                    AutoSelectExpiredReason();
                }

                // Сбрасываем форму и возвращаем фокус на сканер
                ResetScanField();

                UpdateDocumentTotals();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка добавления", ex.Message);
            }
        }
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        DocumentLine selectedDocumentLine;

        if (sender is MenuItem menuItem)
            selectedDocumentLine = dgItems.SelectedItem as DocumentLine;
        else if (dgItems.SelectedItem is DocumentLine selectedLine)
            selectedDocumentLine = selectedLine;
        else
        {
            ShowInfo("Выберите позицию для редактирования");
            return;
        }

        if (selectedDocumentLine == null) return;

        // Загружаем данные позиции в форму для редактирования
        var product = selectedDocumentLine.Product;
        if (product != null)
        {
            cmbProduct.SelectedItem = _allProductsObservable.FirstOrDefault(p => p.Id == product.Id);
        }

        if (selectedDocumentLine.SourceBatchId.HasValue)
        {
            var batch = _availableBatches.FirstOrDefault(b => b.Id == selectedDocumentLine.SourceBatchId.Value);
            if (batch != null)
            {
                cmbBatchSeries.SelectedItem = batch;
            }
        }

        txtQuantity.Text = selectedDocumentLine.Quantity.ToString();

        // Устанавливаем причину списания
        var notes = selectedDocumentLine.Notes ?? "";
        if (notes.Contains("Списание:"))
        {
            var reason = notes.Split(':')[1].Split('\n')[0].Trim();
        }

        // Удаляем позицию из списка
        _documentItems.Remove(selectedDocumentLine);
        UpdateDocumentTotals();

        ShowInfo("Позиция готова к редактированию");
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        DocumentLine documentLine;

        if (sender is Button button)
            documentLine = button.Tag as DocumentLine;
        else if (dgItems.SelectedItem is DocumentLine selectedLine)
            documentLine = selectedLine;
        else
            return;

        var productName = documentLine.Product?.Name ?? "товара";

        var result = MessageBox.Show(
            $"Удалить позицию '{productName}' из акта списания?",
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

    private void ResetItemForm()
    {
        cmbProduct.SelectedItem = null;
        cmbBatchSeries.SelectedItem = null;
        cmbBatchSeries.ItemsSource = null;
        txtQuantity.Text = "1";
        txtQuantity.IsEnabled = true;
        cmbBatchSeries.Visibility = Visibility.Visible;
        lblSgtin.Visibility = Visibility.Collapsed;
        cmbSgtin.Visibility = Visibility.Collapsed;
        txtSgtinManual.Visibility = Visibility.Collapsed;
        txtSgtinManual.Text = "";
        cmbSgtin.ItemsSource = null;
        cmbSgtin.SelectedItem = null;

        // Сбрасываем информацию о товаре
        txtProductManufacturer.Text = "-";
        txtCurrentStock.Text = "0";
        txtCurrentPrice.Text = "0.00";
        txtExpirationDate.Text = "-";
        txtAvailableQuantity.Text = "0";
    }

    private void UpdateDocumentTotals()
    {
        _totalQuantity = _documentItems.Sum(item => item.Quantity);
        _documentTotal = _documentItems.Sum(item => item.TotalPrice);

        txtItemsCount.Text = _documentItems.Count.ToString();
        txtTotalQuantity.Text = _totalQuantity.ToString("N0");
        txtTotalAmount.Text = _documentTotal.ToString("N2");
        txtSelectedItemsInfo.Text = $"Выбрано: {dgItems.SelectedItems.Count}";
    }

    #endregion

    #region UI Event Handlers

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProduct.SelectedItem is Product product)
        {
            // Загружаем информацию о товаре
            txtProductManufacturer.Text = product.Manufacturer ?? "-";
            txtCurrentStock.Text = product.CurrentStock.ToString("N0");

            // Берем цену из последней партии
            var latestBatch = _batchService.GetByProduct(product.Id)
                .Where(b => b.IsActive && b.Quantity > 0)
                .OrderByDescending(b => b.ArrivalDate)
                .FirstOrDefault();

            txtCurrentPrice.Text = (latestBatch?.SellingPrice ?? 0m).ToString("N2");

            // Проверяем, маркирован ли товар
            if (product.IsTracked)
            {
                // Для маркированных товаров: показываем SGTIN, скрываем и блокируем количество, скрываем cmbBatchSeries
                lblSgtin.Visibility = Visibility.Visible;
                cmbSgtin.Visibility = Visibility.Visible;
                txtSgtinManual.Visibility = Visibility.Visible;
                txtQuantity.Text = "1";
                txtQuantity.IsEnabled = false;
                cmbBatchSeries.Visibility = Visibility.Collapsed;

                // Загружаем доступные SGTIN для этого товара (статус InCirculation)
                using var db5 = new PharmacyWarehouseContext();
                var availableSgtins = db5.MdlpSgtins
                    .Include(s => s.Batch)
                    .Where(s => s.ProductId == product.Id && s.Status == "InCirculation")
                    .ToList();
                cmbSgtin.ItemsSource = availableSgtins;
                cmbSgtin.SelectedItem = null;
            }
            else
            {
                // Для немаркированных: показываем количество и cmbBatchSeries, скрываем SGTIN
                lblSgtin.Visibility = Visibility.Collapsed;
                cmbSgtin.Visibility = Visibility.Collapsed;
                txtQuantity.IsEnabled = true;
                cmbBatchSeries.Visibility = Visibility.Visible;
                cmbSgtin.ItemsSource = null;
                // Загружаем доступные партии
                LoadAvailableBatches(product.Id);
            }
        }
        else
        {
            ResetItemForm();
        }
    }

    private void CmbBatchSeries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbBatchSeries.SelectedItem is Batch batch)
        {
            _selectedBatch = batch;
            txtExpirationDate.Text = batch.ExpirationDate.ToString("dd.MM.yyyy") ?? "-";
            txtAvailableQuantity.Text = batch.Quantity.ToString("N0");

            // Если партия просрочена, автоматически выбираем причину списания
            if (batch.IsExpired)
            {
                AutoSelectExpiredReason();
            }
        }
        else
        {
            _selectedBatch = null;
            txtExpirationDate.Text = "-";
            txtAvailableQuantity.Text = "0";
        }
    }

    private void LoadAvailableBatches(int productId)
    {
        _availableBatches = _batchService.GetByProduct(productId)
            .Where(b => b.IsActive && b.Quantity > 0)
            .OrderBy(b => b.ExpirationDate) // Сначала партии с ближайшим сроком годности
            .ToList();

        cmbBatchSeries.ItemsSource = _availableBatches;
        if (_availableBatches.Any())
        {
            cmbBatchSeries.SelectedIndex = 0;
        }
    }

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
        txtScanError.Visibility = Visibility.Collapsed;

        // Проверка: это DataMatrix? (если код длинный или содержит специальные символы)
        if (code.Length > 20 || code.Contains("\x1D"))
        {
            ProcessDataMatrix(code);
            return;
        }

        // Проверка: это GTIN (13 или 14 цифр)
        if (code.All(char.IsDigit) && (code.Length == 13 || code.Length == 14))
        {
            ProcessGtin(code);
            return;
        }

        // Если код не распознан
        ShowScanError("Код не распознан. Введите GTIN (13-14 цифр) или сканируйте DataMatrix.");
    }

    private void ProcessDataMatrix(string code)
    {
        var result = DataMatrixParser.Parse(code);
        if (!result.IsValid)
        {
            ShowScanError(result.ErrorMessage ?? "Некорректный DataMatrix код.");
            return;
        }

        if (string.IsNullOrEmpty(result.Sgtin))
        {
            ShowScanError("DataMatrix код не содержит SGTIN.");
            return;
        }

        string gtinFromSgtin = result.Sgtin.Substring(0, 14);
        
        // Проверка на ограничение по GTIN
        if (!string.IsNullOrEmpty(RestrictedGtin) && gtinFromSgtin != RestrictedGtin)
        {
            ShowScanError($"Пожалуйста, сканируйте только выбранный товар (GTIN: {RestrictedGtin})");
            return;
        }

        // Ищем SGTIN в базе
        using var db6 = new PharmacyWarehouseContext();
        var sgtinObj = db6.MdlpSgtins
            .Include(s => s.Batch)
            .FirstOrDefault(s => s.Sgtin == result.Sgtin && s.Status == "InCirculation");

        if (sgtinObj == null)
        {
            ShowScanError("Маркированный товар не найден или не в обороте.");
            return;
        }

        // Get the product via ProductId
        var product = _productsWithStock.FirstOrDefault(p => p.Id == sgtinObj.ProductId);
        if (product == null)
        {
            product = _allProducts.FirstOrDefault(p => p.Id == sgtinObj.ProductId);
        }

        // Проверяем, не добавлен ли уже этот SGTIN в документ
        if (_documentItems.Any(item => item.Sgtin == result.Sgtin))
        {
            ShowScanError("Этот маркированный товар уже добавлен в документ.");
            return;
        }

        // Добавляем маркированный товар автоматически
        var documentLine = new DocumentLine
        {
            ProductId = product.Id,
            Product = product,
            Quantity = 1,
            UnitPrice = sgtinObj.Batch?.SellingPrice ?? 0m,
            SellingPrice = sgtinObj.Batch?.SellingPrice ?? 0m,
            Series = sgtinObj.Batch?.Series,
            ExpirationDate = sgtinObj.Batch?.ExpirationDate,
            SourceBatchId = sgtinObj.Batch?.Id,
            Notes = "Списание",
            Sgtin = result.Sgtin
        };

        _documentItems.Add(documentLine);

        // Если партия просрочена, автоматически выбираем причину списания
        if (sgtinObj.Batch?.IsExpired ?? false)
        {
            AutoSelectExpiredReason();
        }

        UpdateDocumentTotals();

        // Очищаем поле и возвращаем фокус
        ResetScanField();
    }

    private void ProcessGtin(string gtin)
    {
        // Проверка на ограничение по GTIN
        if (!string.IsNullOrEmpty(RestrictedGtin) && gtin != RestrictedGtin)
        {
            ShowScanError($"Пожалуйста, сканируйте только выбранный товар (GTIN: {RestrictedGtin})");
            return;
        }
        
        // Ищем товар по GTIN
        var product = _productsWithStock.FirstOrDefault(p => p.Gtin == gtin);
        if (product == null)
        {
            ShowScanError("Товар с таким GTIN не найден или нет на складе.");
            return;
        }

        // Выбираем товар в комбобоксе
        cmbProduct.SelectedItem = product;

        // Для маркированных товаров нужно выбрать SGTIN, но мы оставим поле сканирования с фокусом
        if (product.IsTracked)
        {
            txtScanError.Text = "Товар найден. Пожалуйста, сканируйте DataMatrix для выбора конкретной упаковки.";
            txtScanError.Foreground = System.Windows.Media.Brushes.Blue;
            txtScanError.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(() => txtScanCode.Focus());
            return;
        }

        // Для немаркированных товаров: подготавливаем форму к добавлению
        if (int.TryParse(txtQuantity.Text, out int quantity) && quantity <= 0)
            quantity = 1;

        // Загружаем доступные партии
        LoadAvailableBatches(product.Id);

        // Проверяем, есть ли просроченная партия, чтобы автоматом выбрать причину
        var earliestBatch = _batchService.GetByProduct(product.Id)
            .Where(b => b.IsActive && b.Quantity > 0)
            .OrderBy(b => b.ExpirationDate)
            .FirstOrDefault();

        if (earliestBatch?.IsExpired ?? false)
        {
            AutoSelectExpiredReason();
        }

        txtScanCode.Text = "";
        txtScanError.Visibility = Visibility.Collapsed;
        Dispatcher.BeginInvoke(() => txtQuantity.Focus());
        txtQuantity.SelectAll();
    }

    private void ShowScanError(string message)
    {
        txtScanError.Text = message;
        txtScanError.Visibility = Visibility.Visible;
        // Не очищаем поле, чтобы пользователь видел, что не распозналось
        Dispatcher.BeginInvoke(() => txtScanCode.Focus());
    }

    private void ResetScanField()
    {
        txtScanCode.Text = "";
        txtScanError.Visibility = Visibility.Collapsed;
        ResetItemForm();
        Dispatcher.BeginInvoke(() => txtScanCode.Focus());
    }

    private void AutoSelectExpiredReason()
    {
        // Ищем причину "Просроченный срок годности" в комбобоксе
        foreach (ComboBoxItem item in cmbReason.Items)
        {
            if (item.Content?.ToString()?.Contains("Просроченный") ?? false)
            {
                cmbReason.SelectedItem = item;
                break;
            }
        }
    }

    private void DgItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        txtSelectedItemsInfo.Text = $"Выбрано: {dgItems.SelectedItems.Count}";
    }

    // Валидация числового ввода
    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    #endregion

    #region Document Operations

    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateDocument())
            return;

        try
        {
            // Обновляем данные документа
            _currentDocument.Date = dpWriteOffDate.SelectedDate ?? DateTime.Now;
            _currentDocument.CustomerName = txtResponsiblePerson.Text;
            if (cmbReason.SelectedItem is ComboBoxItem selectedReasonItem)
            {
                _currentDocument.WriteOffReason = (WriteOffReason?)selectedReasonItem.Tag;
            }
            _currentDocument.Notes = $"{((ComboBoxItem)cmbReason.SelectedItem)?.Content ?? "Не указана"}" +
                                   (!string.IsNullOrWhiteSpace(txtNotes.Text) ? $"\n{txtNotes.Text}" : "") +
                                   (!string.IsNullOrWhiteSpace(txtCommission.Text) ? $"\n{txtCommission.Text}" : "");
            _currentDocument.Status = DocumentStatus.Draft;
            _currentDocument.Amount = _documentTotal;

            // Создаем список строк документа (не копируем ID, сервис создаёт новые строки)
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
                    SourceBatchId = item.SourceBatchId,
                    Notes = item.Notes,
                    Sgtin = item.Sgtin
                };

                documentLines.Add(line);
            }

            // Сохраняем документ
            int documentId = _documentService.CreateWriteOff(_currentDocument, documentLines);

            // Обновляем ID текущего документа и UI
            _currentDocument.Id = documentId;
            txtDocNumber.Text = _currentDocument.Number;

            ShowInfo($"Черновик акта списания сохранен. Номер: {_currentDocument.Number}");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка сохранения", ex.Message);
        }
    }

    private async void ProcessWriteOff_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateDocument())
            return;

        if (_documentItems.Count == 0)
        {
            ShowError("Ошибка", "Добавьте хотя бы одну позицию в акт списания");
            return;
        }

        var result = MessageBox.Show(
            $"Провести акт списания №{_currentDocument.Number}?\n\n" +
            $"Количество позиций: {_documentItems.Count}\n" +
            $"Общая сумма списания: {_documentTotal:N2} руб.\n" +
            $"Причина: {((ComboBoxItem)cmbReason.SelectedItem)?.Content ?? "Не указана"}",
            "Проведение акта списания",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Обновляем данные документа
                _currentDocument.Date = dpWriteOffDate.SelectedDate ?? DateTime.Now;
                _currentDocument.CustomerName = txtResponsiblePerson.Text;
                if (cmbReason.SelectedItem is ComboBoxItem selectedReasonItem)
                {
                    _currentDocument.WriteOffReason = (WriteOffReason?)selectedReasonItem.Tag;
                }
                _currentDocument.Notes = $"{((ComboBoxItem)cmbReason.SelectedItem)?.Content ?? "Не указана"}" +
                                       (!string.IsNullOrWhiteSpace(txtNotes.Text) ? $"\n{txtNotes.Text}" : "") +
                                       (!string.IsNullOrWhiteSpace(txtCommission.Text) ? $"\n{txtCommission.Text}" : "");
                _currentDocument.Status = DocumentStatus.Processed;
                _currentDocument.Amount = _documentTotal;
                _currentDocument.SignedBy = _currentUser;
                _currentDocument.SignedAt = DateTime.Now;

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
                        SourceBatchId = item.SourceBatchId,
                        Notes = item.Notes,
                        Sgtin = item.Sgtin
                    };
                    documentLines.Add(line);
                }

                int documentId;
                if (_currentDocument.Id == 0) // Новый документ
                {
                    documentId = _documentService.CreateWriteOff(_currentDocument, documentLines);
                    _currentDocument.Id = documentId;
                    txtDocNumber.Text = _currentDocument.Number;
                }
                else // Редактирование существующего
                {
                    // Для списания используем тот же метод, что и для расходных накладных
                    documentId = _documentService.CreateWriteOff(_currentDocument, documentLines);
                    _currentDocument.Id = documentId;
                }

                // Проверяем, был ли документ уже отправлен в МДЛП
                using var db7 = new PharmacyWarehouseContext();
                var existingMdlpDoc = await db7.MdlpDocuments
                    .FirstOrDefaultAsync(d => d.DocumentId == _currentDocument.Id);

                if (existingMdlpDoc != null)
                {
                    MessageBox.Show($"Документ уже отправлен в МДЛП.\nСтатус: {existingMdlpDoc.StatusText}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    InitializeNewDocument();
                    return;
                }

                // Вызываем МДЛП сервис
                var mdlpResult = await _mdlpService.SendWriteOffAsync(_currentDocument);
                string mdlpStatusText = mdlpResult.Status == "Accepted" ? "Принято" : "Ожидает обработки";

                MessageBox.Show($"Акт списания проведен успешно!\nНомер: {_currentDocument.Number}\nID: {documentId}\nМДЛП: {mdlpStatusText} (ID: {mdlpResult.MdlpDocumentId})",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                
                NavigateToDocumentsPage();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка проведения документа", ex.Message);
            }
        }
    }

    private bool ValidateDocument()
    {
        if (cmbReason.SelectedItem == null)
        {
            ShowError("Ошибка", "Выберите причину списания");
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtResponsiblePerson.Text))
        {
            ShowError("Ошибка", "Введите ФИО ответственного");
            return false;
        }

        return true;
    }

    private void CancelDocument_Click(object sender, RoutedEventArgs e)
    {
        if (_documentItems.Count > 0)
        {
            var result = MessageBox.Show(
                "Отменить создание акта списания?\nВсе несохраненные данные будут потеряны.",
                "Отмена документа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        InitializeNewDocument();
        ShowInfo("Акт списания отменен");
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

    #endregion

    #region Additional Features

    private void CopyNumber_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_currentDocument.Number);
        ShowInfo($"Номер акта скопирован: {_currentDocument.Number}");
    }

    private void ShowStatistics_Click(object sender, RoutedEventArgs e)
    {
        var stats = new System.Text.StringBuilder();
        stats.AppendLine($"📊 Статистика акта списания:");
        stats.AppendLine($"• Позиций: {_documentItems.Count}");
        stats.AppendLine($"• Общее количество: {_totalQuantity}");
        stats.AppendLine($"• Общая сумма списания: {_documentTotal:N2} руб.");

        // Подсчет по причинам списания
        var reasons = new Dictionary<string, int>();
        foreach (var item in _documentItems)
        {
            var reason = "Не указана";
            if (item.Notes?.Contains("Списание:") == true)
            {
                reason = item.Notes.Split(':')[1].Split('\n')[0].Trim();
            }

            if (reasons.ContainsKey(reason))
                reasons[reason] += item.Quantity;
            else
                reasons[reason] = item.Quantity;
        }

        stats.AppendLine($"• Распределение по причинам:");
        foreach (var reason in reasons)
        {
            stats.AppendLine($"  - {reason.Key}: {reason.Value} шт.");
        }

        MessageBox.Show(stats.ToString(), "Статистика документа",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PrintDocument_Click(object sender, RoutedEventArgs e)
    {
        if (_documentItems.Count == 0)
        {
            ShowError("Ошибка", "Акт списания пуст");
            return;
        }

        ShowInfo("Функция печати будет реализована позже");
    }

    private void ShowProductInfo_Click(object sender, RoutedEventArgs e)
    {
        if (dgItems.SelectedItem is DocumentLine documentLine && documentLine.Product != null)
        {
            var product = documentLine.Product;
            var info = new System.Text.StringBuilder();
            info.AppendLine($"ℹ️ Информация о товаре:");
            info.AppendLine($"• Название: {product.Name}");
            info.AppendLine($"• Производитель: {product.Manufacturer}");
            info.AppendLine($"• Категория: {product.Category?.Name ?? "Без категории"}");
            info.AppendLine($"• Форма выпуска: {product.ReleaseForm}");
            info.AppendLine($"• Ед. измерения: {product.UnitOfMeasure}");
            info.AppendLine($"• Текущий остаток: {product.CurrentStock} шт.");
            info.AppendLine($"• Минимальный остаток: {product.MinRemainder} шт.");
            info.AppendLine($"• Рецептурный: {(product.RequiresPrescription ? "Да" : "Нет")}");

            if (documentLine.SourceBatchId.HasValue)
            {
                var batch = _batchService.GetById(documentLine.SourceBatchId.Value);
                if (batch != null)
                {
                    info.AppendLine($"• Серия партии: {batch.Series}");
                    info.AppendLine($"• Срок годности: {batch.ExpirationDate:dd.MM.yyyy}");
                }
            }

            MessageBox.Show(info.ToString(), "Информация о товаре",
                MessageBoxButton.OK, MessageBoxImage.Information);
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