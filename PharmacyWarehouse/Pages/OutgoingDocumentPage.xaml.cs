using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.Mdlp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PharmacyWarehouse.Pages;

public partial class OutgoingPage : Page, INotifyPropertyChanged
{
    private readonly DocumentService _documentService;
    private readonly ProductService _productService;
    private readonly BatchService _batchService;
    private readonly AuthService _authService;
    private readonly IMdlpService _mdlpService;

    private Document _currentDocument;
    private ObservableCollection<DocumentLine> _documentItems;
    private bool _hasPrescriptionItems = false;

    private ICollectionView _filteredProductsView;
    private string _productSearchText = string.Empty;
    private ObservableCollection<Product> _allProductsObservable = new();
    private List<Product> _productsWithStock = new();

    public event PropertyChangedEventHandler PropertyChanged;

    private decimal _documentTotal = 0m;
    private int _totalQuantity = 0;
    private string _currentUser = String.Empty;


    public OutgoingPage(int? documentId = null)
    {
        InitializeComponent();
        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _batchService = App.ServiceProvider.GetService<BatchService>();
        _authService = App.ServiceProvider.GetService<AuthService>();
        _mdlpService = App.ServiceProvider.GetService<IMdlpService>();

        _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";

        DataContext = this;
        Loaded += (s, e) =>
        {
            LoadProducts();
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

    private void LoadProducts()
    {
        try
        {
            _allProductsObservable.Clear();

            var allProducts = _productService.Products
                .Where(p => p.IsActive && !p.IsSalesBlocked)
                .ToList();

            foreach (var product in allProducts)
            {
                var batches = _batchService.GetByProduct(product.Id)
                    .Where(b => b.IsActive && b.Quantity > 0)
                    .ToList();

                _allProductsObservable.Add(product);

                var hasStock = _batchService.GetByProduct(product.Id)
                .Any(b => b.IsActive && b.Quantity > 0);

                if (hasStock)
                    _productsWithStock.Add(product);
            }

            _filteredProductsView = CollectionViewSource.GetDefaultView(_allProductsObservable);
            _filteredProductsView.Filter = FilterProduct;

            cmbProduct.ItemsSource = _productsWithStock;
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки товаров", ex.Message);
        }
    }

    private void InitializeNewDocument()
    {
        _currentDocument = new Document
        {
            Type = DocumentType.Outgoing,
            Date = DateTime.Now,
            Status = DocumentStatus.Draft,
            CreatedBy = _currentUser,
            CreatedAt = DateTime.Now,
            Number = string.Empty // Let CreateOutgoing generate it
        };

        txtDocNumber.Text = "(будет сгенерирован при сохранении)";
        dpDocDate.SelectedDate = _currentDocument.Date;

        btnProcessDocument.IsEnabled = true;
        txtAlreadySentHint.Visibility = Visibility.Collapsed;

        _documentItems.Clear();
        customerInfoPanel.Visibility = Visibility.Collapsed;
        _hasPrescriptionItems = false;
        txtSgtinManual.Text = "";
        txtSgtinManual.Visibility = Visibility.Collapsed;

        // Сбрасываем сканер
        txtScanCode.Text = "";
        txtScanError.Visibility = Visibility.Collapsed;

        UpdateDocumentTotals();
        UpdateLineTotal();

        // Устанавливаем фокус на поле сканирования
        Dispatcher.BeginInvoke(() => txtScanCode.Focus());
    }

    private async void LoadDraft(int documentId)
    {
        try
        {
            var draft = _documentService.GetById(documentId);
            if (draft == null || draft.Type != DocumentType.Outgoing)
                return;

            _currentDocument = draft;

            // Проверяем, был ли документ уже отправлен в МДЛП
            using var dbDraft = new PharmacyWarehouseContext();
            var existingMdlpDoc = await dbDraft.MdlpDocuments
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

            // Заполняем поля формы
            txtDocNumber.Text = draft.Number;
            dpDocDate.SelectedDate = draft.Date;
            txtCustomerName.Text = draft.CustomerName;
            txtPrescriptionNumber.Text = draft.CustomerDocument;
            txtNotes.Text = draft.Notes;

            // Загружаем позиции
            _documentItems.Clear();
            foreach (var line in draft.DocumentLines)
            {
                var product = _allProductsObservable.FirstOrDefault(p => p.Id == line.ProductId);
                line.Product = product;
                _documentItems.Add(line);
            }

            // Проверяем наличие рецептурных товаров
            CheckPrescriptionItems();

            UpdateDocumentTotals();
            ShowInfo("Черновик загружен");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки черновика", ex.Message);
            InitializeNewDocument();
        }
    }

    private string GenerateDocumentNumber()
    {
        var date = DateTime.Now;
        var count = _documentService.GetDocumentsCount(date.Month, date.Year, PharmacyWarehouse.Models.DocumentType.Outgoing);
        return $"РН-{date:MMyy}-{count + 1:D4}";
    }

    #endregion

    #region Document Items Management

    private bool FilterProduct(object obj)
    {
        if (obj is not Product product || string.IsNullOrWhiteSpace(ProductSearchText))
            return true;

        var searchText = ProductSearchText.ToLower();
        return product.Name.ToLower().Contains(searchText) ||
               (product.Manufacturer?.ToLower().Contains(searchText) ?? false) ||
               (product.Description?.ToLower().Contains(searchText) ?? false);
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        DocumentLine selectedDocumentLine;

        if (sender is Button button)
            selectedDocumentLine = button.Tag as DocumentLine;
        else if (dgItems.SelectedItem is DocumentLine selectedLine)
            selectedDocumentLine = selectedLine;
        else
        {
            ShowInfo("Выберите позицию для редактирования");
            return;
        }

        var product = selectedDocumentLine.Product;
        if (product != null)
        {
            cmbProduct.SelectedItem = _allProductsObservable.FirstOrDefault(p => p.Id == product.Id);
        }

        txtQuantity.Text = selectedDocumentLine.Quantity.ToString();

        _documentItems.Remove(selectedDocumentLine);

        // Проверяем наличие рецептурных товаров
        CheckPrescriptionItems();

        UpdateDocumentTotals();
        UpdateLineTotal();

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
            $"Удалить позицию '{productName}'?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _documentItems.Remove(documentLine);

            // Проверяем наличие рецептурных товаров
            CheckPrescriptionItems();

            UpdateDocumentTotals();
            ShowInfo("Позиция удалена");
        }
    }

    private void CheckPrescriptionItems()
    {
        _hasPrescriptionItems = _documentItems.Any(item =>
            item.Product != null && item.Product.RequiresPrescription);

        if (_hasPrescriptionItems)
        {
            customerInfoPanel.Visibility = Visibility.Visible;
        }
        else
        {
            customerInfoPanel.Visibility = Visibility.Collapsed;
            txtCustomerName.Text = "";
            txtPrescriptionNumber.Text = "";
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
        if (txtQuantity == null || txtSellingPrice == null || txtLineTotal == null)
            return;

        if (int.TryParse(txtQuantity.Text, out int quantity) &&
            decimal.TryParse(txtSellingPrice.Text, out decimal price))
        {
            txtLineTotal.Text = (quantity * price).ToString("N2");
        }
        else
        {
            txtLineTotal.Text = "0.00";
        }
    }

    #endregion

    #region Document Operations

    private async void ProcessDocument_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateDocument())
            return;

        if (_documentItems.Count == 0)
        {
            ShowError("Ошибка", "Добавьте хотя бы одну позицию в документ");
            return;
        }

        // Проверка доступности товаров
        var products = _productService.Products.ToList();
        var insufficientItems = new List<string>();
        foreach (var item in _documentItems)
        {
            // Ensure Product is set on the item
            item.Product = products.FirstOrDefault(p => p.Id == item.ProductId);

            // Skip batch quantity check for tracked products (IsTracked = true)
            if (item.Product != null && item.Product.IsTracked)
            {
                // For tracked products, we assume each SGTIN in the document is valid (we already checked when adding)
                continue;
            }

            // For non-tracked products, check batch quantities as before
            var availableBatches = _batchService.GetByProduct(item.ProductId)
                .Where(b => b.IsActive && b.Quantity > 0)
                .ToList();
            var totalAvailable = availableBatches.Sum(b => b.Quantity);

            if (item.Quantity > totalAvailable)
            {
                insufficientItems.Add($"'{item.Product?.Name}' - нужно {item.Quantity}, доступно {totalAvailable}");
            }
        }

        if (insufficientItems.Any())
        {
            var errorMessage = "Недостаточно товаров на складе:\n" +
                              string.Join("\n", insufficientItems);
            ShowError("Ошибка", errorMessage);
            return;
        }

        var result = MessageBox.Show(
            $"Провести расходную накладную №{_currentDocument.Number}?\n\n" +
            $"Позиций: {_documentItems.Count}\n" +
            $"Общая сумма: {_documentTotal:N2} руб.\n" +
            (_hasPrescriptionItems ? $"Клиент: {txtCustomerName.Text}\n" : ""),
            "Проведение документа",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Создаем или обновляем документ
                if (_currentDocument.Id == 0) // Новый документ
                {
                    _currentDocument.Type = DocumentType.Outgoing;
                    _currentDocument.Number = txtDocNumber.Text;
                    _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
                    _currentDocument.CustomerName = _hasPrescriptionItems ? txtCustomerName.Text : null;
                    _currentDocument.CustomerDocument = _hasPrescriptionItems ? txtPrescriptionNumber.Text : null;
                    _currentDocument.Notes = txtNotes.Text;
                    _currentDocument.Status = DocumentStatus.Processed;
                    _currentDocument.CreatedBy = _currentUser;
                    _currentDocument.CreatedAt = DateTime.Now;
                    _currentDocument.Amount = _documentTotal;
                    _currentDocument.SignedBy = _currentUser;
                    _currentDocument.SignedAt = DateTime.Now;
                }
                else // Редактирование существующего
                {
                    _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
                    _currentDocument.CustomerName = _hasPrescriptionItems ? txtCustomerName.Text : null;
                    _currentDocument.CustomerDocument = _hasPrescriptionItems ? txtPrescriptionNumber.Text : null;
                    _currentDocument.Notes = txtNotes.Text;
                    _currentDocument.Status = DocumentStatus.Processed;
                    _currentDocument.Amount = _documentTotal;
                    _currentDocument.SignedBy = _currentUser;
                    _currentDocument.SignedAt = DateTime.Now;
                }

                var documentLines = new List<DocumentLine>();
                foreach (var item in _documentItems)
                {
                    // Для маркированных товаров не задаём SourceBatchId — сервис возьмёт его из SGTIN
                    int? sourceBatchId = null;
                    if (item.Product == null || !item.Product.IsTracked)
                    {
                        // Для немаркированных товаров
                        var availableBatches = _batchService.GetByProduct(item.ProductId)
                            .Where(b => b.IsActive && b.Quantity > 0)
                            .OrderBy(b => b.ExpirationDate)
                            .ToList();
                        var batchToUse = availableBatches.FirstOrDefault();
                        sourceBatchId = batchToUse?.Id;
                    }

                    var line = new DocumentLine
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        SellingPrice = item.SellingPrice,
                        SourceBatchId = sourceBatchId,
                        Notes = $"Отпуск покупателю" +
                               (_hasPrescriptionItems ? $", клиент: {txtCustomerName.Text}" : ""),
                        Sgtin = item.Sgtin
                    };
                    documentLines.Add(line);
                }

                int documentId = _documentService.CreateOutgoing(_currentDocument, documentLines);
                _currentDocument.Id = documentId;
                txtDocNumber.Text = _currentDocument.Number;

                // Проверяем, был ли документ уже отправлен в МДЛП
                using var dbProcess = new PharmacyWarehouseContext();
                var existingMdlpDocProc = await dbProcess.MdlpDocuments
                    .FirstOrDefaultAsync(d => d.DocumentId == _currentDocument.Id);

                if (existingMdlpDocProc != null)
                {
                    MessageBox.Show($"Документ уже отправлен в МДЛП.\nСтатус: {existingMdlpDocProc.StatusText}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    InitializeNewDocument();
                    return;
                }

                // Вызываем МДЛП сервис
                var mdlpResult = await _mdlpService.SendOutgoingAsync(_currentDocument);
                string mdlpStatusText = mdlpResult.Status == "Accepted" ? "Принято" : "Ожидает обработки";

                MessageBox.Show($"Документ проведен успешно!\nНомер: {_currentDocument.Number}\nID: {documentId}\nМДЛП: {mdlpStatusText} (ID: {mdlpResult.MdlpDocumentId})",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                
                NavigateToDocumentsPage();
            }
            catch (Exception ex)
            {
                var errorMessage = $"Ошибка проведения документа: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nВнутренняя ошибка: {ex.InnerException.Message}";
                }
                ShowError("Ошибка проведения документа", errorMessage);
            }
        }
    }

    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateDocument())
            return;

        try
        {
            // Создаем или обновляем документ как черновик
            if (_currentDocument.Id == 0) // Новый документ
            {
                _currentDocument.Type = DocumentType.Outgoing;
                _currentDocument.Number = txtDocNumber.Text; // Номер уже сгенерирован
                _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
                _currentDocument.CustomerName = _hasPrescriptionItems ? txtCustomerName.Text : null;
                _currentDocument.CustomerDocument = _hasPrescriptionItems ? txtPrescriptionNumber.Text : null;
                _currentDocument.Notes = txtNotes.Text;
                _currentDocument.Status = DocumentStatus.Draft;
                _currentDocument.CreatedBy = _currentUser;
                _currentDocument.CreatedAt = DateTime.Now;
                _currentDocument.Amount = _documentTotal;
            }
            else // Редактирование существующего
            {
                _currentDocument.Date = dpDocDate.SelectedDate ?? DateTime.Now;
                _currentDocument.CustomerName = _hasPrescriptionItems ? txtCustomerName.Text : null;
                _currentDocument.CustomerDocument = _hasPrescriptionItems ? txtPrescriptionNumber.Text : null;
                _currentDocument.Notes = txtNotes.Text;
                _currentDocument.Status = DocumentStatus.Draft;
                _currentDocument.Amount = _documentTotal;
            }

            var documentLines = new List<DocumentLine>();
            foreach (var item in _documentItems)
            {
                var line = new DocumentLine
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    SellingPrice = item.SellingPrice,
                    Notes = $"Отпуск покупателю (черновик)" +
                           (_hasPrescriptionItems ? $", клиент: {txtCustomerName.Text}" : ""),
                    Sgtin = item.Sgtin
                };
                documentLines.Add(line);
            }

            int documentId = _documentService.CreateOutgoing(_currentDocument, documentLines);

            // Обновляем ID текущего документа и UI, если это новый черновик
            if (_currentDocument.Id == 0)
            {
                _currentDocument.Id = documentId;
                txtDocNumber.Text = _currentDocument.Number;
            }

            ShowInfo($"Черновик сохранен. Номер документа: {_currentDocument.Number}\nID: {documentId}");
        }
        catch (Exception ex)
        {
            var errorMessage = $"Ошибка сохранения черновика: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nВнутренняя ошибка: {ex.InnerException.Message}";
            }
            ShowError("Ошибка сохранения", errorMessage);
        }
    }
    private bool ValidateDocument()
    {
        // Если есть рецептурные товары, проверяем заполнение полей клиента
        if (_hasPrescriptionItems)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text))
            {
                ShowError("Ошибка", "Введите ФИО клиента");
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtPrescriptionNumber.Text))
            {
                ShowError("Ошибка", "Введите номер рецепта");
                return false;
            }
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

    #region UI Event Handlers

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

        // Ищем SGTIN в базе
        using var db = new PharmacyWarehouseContext();
            var sgtinObj = db.MdlpSgtins
                .Include(s => s.Batch)
                .FirstOrDefault(s => s.Sgtin == result.Sgtin && s.Status == "InCirculation");

        if (sgtinObj == null)
        {
            ShowScanError("Маркированный товар не найден или не в обороте.");
            return;
        }

        // Now get the product via ProductId
        var product = _productsWithStock.FirstOrDefault(p => p.Id == sgtinObj.ProductId);
        if (product == null)
        {
            product = _allProductsObservable.FirstOrDefault(p => p.Id == sgtinObj.ProductId);
        }

        // Проверка на просроченный товар
        if (sgtinObj.Batch != null && sgtinObj.Batch.IsExpired)
        {
            ShowScanError("Товар просрочен, необходимо списать.");
            return;
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
            Notes = "Отпуск покупателю",
            Sgtin = result.Sgtin
        };

        _documentItems.Add(documentLine);
        CheckPrescriptionItems();
        UpdateDocumentTotals();

        // Очищаем поле и возвращаем фокус
        ResetScanField();
    }

    private void ProcessGtin(string gtin)
    {
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

    private void ResetItemForm()
    {
        txtQuantity.Text = "1";
        cmbProduct.SelectedItem = null;
        cmbSgtin.SelectedItem = null;
        txtSgtinManual.Text = "";
        txtSgtinManual.Visibility = Visibility.Collapsed;
        txtSellingPrice.Text = "0.00";
        lblSgtin.Visibility = Visibility.Collapsed;
        cmbSgtin.Visibility = Visibility.Collapsed;
        txtQuantity.IsEnabled = true;
        cmbSgtin.ItemsSource = null;
        UpdateLineTotal();
    }

    // Переопределяем AddItem_Click для объединения строк немаркированных товаров
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

        // Валидация для маркированных товаров
        if (selectedProduct.IsTracked)
        {
            string sgtin = null;
            if (cmbSgtin.SelectedItem is MdlpSgtin selectedSgtin)
            {
                sgtin = selectedSgtin.Sgtin;
            }
            else if (!string.IsNullOrWhiteSpace(txtSgtinManual.Text))
            {
                sgtin = txtSgtinManual.Text.Trim();
                // Проверяем, что такой SGTIN существует и имеет нужный статус
                using var db2 = new PharmacyWarehouseContext();
                var existingSgtin = db2.MdlpSgtins.FirstOrDefault(s => s.Sgtin == sgtin && s.Status == "InCirculation" && s.ProductId == selectedProduct.Id);
                if (existingSgtin == null)
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
                var earliestBatch = _batchService.GetByProduct(selectedProduct.Id)
                    .Where(b => b.IsActive && b.Quantity > 0)
                    .OrderBy(b => b.ExpirationDate)
                    .FirstOrDefault();
                var sellingPrice = earliestBatch?.SellingPrice ?? 0m;

                var documentLine = new DocumentLine
                {
                    ProductId = selectedProduct.Id,
                    Product = selectedProduct,
                    Quantity = 1,
                    UnitPrice = sellingPrice,
                    SellingPrice = sellingPrice,
                    Notes = "Отпуск покупателю",
                    Sgtin = sgtin
                };

                _documentItems.Add(documentLine);

                // Сбрасываем форму и возвращаем фокус на сканер
                ResetScanField();

                CheckPrescriptionItems();
                UpdateDocumentTotals();
                ShowInfo($"Товар '{selectedProduct.Name}' добавлен с SGTIN: {sgtin}");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка добавления", ex.Message);
            }
        }
        else
        {
            // Для немаркированных товаров
            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                ShowError("Ошибка", "Введите корректное количество");
                return;
            }

            // Проверка доступного количества
            var availableBatches = _batchService.GetByProduct(selectedProduct.Id)
                .Where(b => b.IsActive && b.Quantity > 0)
                .OrderBy(b => b.ExpirationDate)
                .ToList();

            var totalAvailable = availableBatches.Sum(b => b.Quantity);

            // Учитываем уже добавленные в документ количества
            var alreadyAdded = _documentItems
                .Where(item => item.ProductId == selectedProduct.Id && string.IsNullOrEmpty(item.Sgtin))
                .Sum(item => item.Quantity);

            var totalNeeded = alreadyAdded + quantity;

            if (totalNeeded > totalAvailable)
            {
                ShowError("Ошибка",
                    $"Недостаточно товара на складе.\n" +
                    $"Доступно: {totalAvailable - alreadyAdded} шт.\n" +
                    $"Уже добавлено: {alreadyAdded} шт.\n" +
                    $"Нужно еще: {quantity} шт.\n" +
                    $"Всего требуется: {totalNeeded} шт.");
                return;
            }

            try
            {
                var earliestBatch = availableBatches.FirstOrDefault();
                var sellingPrice = earliestBatch?.SellingPrice ?? 0m;

                // Пытаемся найти существующую строку с тем же товаром (немаркированным) и объединяем
                var existingLine = _documentItems
                    .FirstOrDefault(item => item.ProductId == selectedProduct.Id && string.IsNullOrEmpty(item.Sgtin));

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
                        UnitPrice = sellingPrice,
                        SellingPrice = sellingPrice,
                        Notes = "Отпуск покупателю"
                    };

                    _documentItems.Add(documentLine);
                    ShowInfo($"Товар '{selectedProduct.Name}' добавлен");
                }

                // Сбрасываем форму и возвращаем фокус на сканер
                ResetScanField();

                CheckPrescriptionItems();
                UpdateDocumentTotals();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка добавления", ex.Message);
            }
        }
    }

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProduct.SelectedItem is Product product)
        {
            var earliestBatch = _batchService.GetByProduct(product.Id)
                .Where(b => b.IsActive && b.Quantity > 0)
                .OrderBy(b => b.ExpirationDate)
                .FirstOrDefault();

            if (earliestBatch != null)
            {
                // Берем цену из партии с самым ранним сроком годности
                txtSellingPrice.Text = earliestBatch.SellingPrice.ToString("N2");

                var totalAvailable = _batchService.GetByProduct(product.Id)
                    .Where(b => b.IsActive && b.Quantity > 0)
                    .Sum(b => b.Quantity);
                ShowInfo($"Доступно на складе: {totalAvailable} шт.\n" +
                        $"Будет списано из партии: {earliestBatch.Series}\n" +
                        $"Срок годности: {earliestBatch.ExpirationDate:dd.MM.yyyy}");
            }
            else
            {
                txtSellingPrice.Text = "0.00";
                ShowInfo($"Товар '{product.Name}' отсутствует на складе");
            }

            // Проверяем, маркирован ли товар
            if (product.IsTracked)
            {
                // Для маркированных товаров: показываем SGTIN, скрываем количество, устанавливаем количество=1
                lblSgtin.Visibility = Visibility.Visible;
                cmbSgtin.Visibility = Visibility.Visible;
                txtSgtinManual.Visibility = Visibility.Visible;
                txtQuantity.Text = "1";
                txtQuantity.IsEnabled = false;

                // Загружаем доступные SGTIN для этого товара (статус InCirculation)
                using var db3 = new PharmacyWarehouseContext();
                var availableSgtins = db3.MdlpSgtins
                    .Include(s => s.Batch)
                    .Where(s => s.ProductId == product.Id && s.Status == "InCirculation")
                    .ToList();
                cmbSgtin.ItemsSource = availableSgtins;
                cmbSgtin.SelectedItem = null;
            }
            else
            {
                // Для немаркированных: показываем количество, скрываем SGTIN
                lblSgtin.Visibility = Visibility.Collapsed;
                cmbSgtin.Visibility = Visibility.Collapsed;
                txtQuantity.IsEnabled = true;
                cmbSgtin.ItemsSource = null;
            }

            UpdateLineTotal();

        }
        else
        {
            lblSgtin.Visibility = Visibility.Collapsed;
            cmbSgtin.Visibility = Visibility.Collapsed;
            txtQuantity.IsEnabled = true;
            cmbSgtin.ItemsSource = null;
        }
    }

    // Валидация числового ввода
    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
        UpdateLineTotal();
    }

    private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
    {
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
        stats.AppendLine($"• Позиций: {_documentItems.Count}");
        stats.AppendLine($"• Общее количество: {_totalQuantity}");
        stats.AppendLine($"• Общая сумма: {_documentTotal:N2} руб.");

        if (_hasPrescriptionItems)
        {
            stats.AppendLine($"• Рецептурных товаров: {_documentItems.Count(i => i.Product?.RequiresPrescription == true)}");
        }

        stats.AppendLine($"• Средний чек: {(_documentItems.Count > 0 ? _documentTotal / _documentItems.Count : 0):N2} руб.");

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
            var batch = documentLine.CreatedBatch;
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
            info.AppendLine($"• Цена продажи: {batch.SellingPrice:N2} руб.");

            MessageBox.Show(info.ToString(), "Информация о товаре",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ShowAvailableBatches_Click(object sender, RoutedEventArgs e)
    {
        if (dgItems.SelectedItem is DocumentLine documentLine && documentLine.Product != null)
        {
            try
            {
                var batches = _batchService.GetByProduct(documentLine.ProductId)
                    .Where(b => b.IsActive && b.Quantity > 0)
                    .OrderBy(b => b.ExpirationDate)
                    .ToList(); 

                var batchInfo = new StringBuilder();
                batchInfo.AppendLine($"📦 Доступные партии для товара:");
                batchInfo.AppendLine($"• {documentLine.Product.Name}");
                batchInfo.AppendLine($"• Общий остаток: {documentLine.Product.CurrentStock} шт.");
                batchInfo.AppendLine();

                var batchToUse = batches.FirstOrDefault();
                if (batchToUse != null)
                {
                    batchInfo.AppendLine($"• Для списания будет использоваться: {batchToUse.Id}");
                }

                batchInfo.AppendLine();

                if (batches.Any())
                {
                    batchInfo.AppendLine("Список партий (сортировка по сроку годности):");
                    foreach (var batch in batches.OrderBy(b => b.ExpirationDate))
                    {
                        batchInfo.AppendLine($"• Партия: {batch.Series}");
                        batchInfo.AppendLine($"  Количество: {batch.Quantity} шт.");
                        batchInfo.AppendLine($"  Срок годности: {batch.ExpirationDate:dd.MM.yyyy}");
                        batchInfo.AppendLine($"  Цена: {batch.SellingPrice:N2} руб.");
                        batchInfo.AppendLine();
                    }
                }
                else
                {
                    batchInfo.AppendLine("Нет доступных партий");
                }

                MessageBox.Show(batchInfo.ToString(), "Доступные партии",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Ошибка", ex.Message);
            }
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
