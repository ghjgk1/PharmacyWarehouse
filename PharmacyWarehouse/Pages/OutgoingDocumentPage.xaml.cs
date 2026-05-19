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
    private readonly PharmacyWarehouseContext _context = BaseDbService.Instance.Context;

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
            Number = GenerateDocumentNumber()
        };

        txtDocNumber.Text = _currentDocument.Number;
        dpDocDate.SelectedDate = _currentDocument.Date;

        btnProcessDocument.IsEnabled = true;
        txtAlreadySentHint.Visibility = Visibility.Collapsed;

        _documentItems.Clear();
        customerInfoPanel.Visibility = Visibility.Collapsed;
        _hasPrescriptionItems = false;

        UpdateDocumentTotals();
        UpdateLineTotal();
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
            var existingMdlpDoc = await _context.MdlpDocuments
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
        var count = _documentService.GetDocumentsCount(date.Month, date.Year);
        return $"РН-{date:MMyy}-{count + 1:D4}";
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

        // Валидация для маркированных товаров
        if (selectedProduct.IsTracked)
        {
            if (cmbSgtin.SelectedItem is not MdlpSgtin selectedSgtin)
            {
                ShowError("Ошибка", "Выберите код маркировки (SGTIN)");
                return;
            }

            // Проверяем, не использован ли уже этот SGTIN в документе
            var existingInDoc = _documentItems.Any(item => item.Sgtin == selectedSgtin.Sgtin);
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
                    Notes = $"Отпуск покупателю",
                    Sgtin = selectedSgtin.Sgtin
                };

                _documentItems.Add(documentLine);

                // Сбрасываем форму
                txtQuantity.Text = "1";
                cmbProduct.SelectedItem = null;
                cmbSgtin.SelectedItem = null;
                txtSellingPrice.Text = "0.00";
                lblSgtin.Visibility = Visibility.Collapsed;
                cmbSgtin.Visibility = Visibility.Collapsed;
                txtQuantity.IsEnabled = true;
                cmbSgtin.ItemsSource = null;

                CheckPrescriptionItems();

                UpdateDocumentTotals();
                UpdateLineTotal();
                ShowInfo($"Товар '{selectedProduct.Name}' добавлен с SGTIN: {selectedSgtin.Sgtin}");
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
                .Where(item => item.ProductId == selectedProduct.Id)
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

                var documentLine = new DocumentLine
                {
                    ProductId = selectedProduct.Id,
                    Product = selectedProduct,
                    Quantity = quantity,
                    UnitPrice = sellingPrice,
                    SellingPrice = sellingPrice,
                    Notes = $"Отпуск покупателю"
                };

                _documentItems.Add(documentLine);

                // Сбрасываем форму
                txtQuantity.Text = "1";
                cmbProduct.SelectedItem = null;
                txtSellingPrice.Text = "0.00";

                CheckPrescriptionItems();

                UpdateDocumentTotals();
                UpdateLineTotal();
                ShowInfo($"Товар '{selectedProduct.Name}' добавлен");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка добавления", ex.Message);
            }
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
        var insufficientItems = new List<string>();
        foreach (var item in _documentItems)
        {
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

                // Проверяем, был ли документ уже отправлен в МДЛП
                var existingMdlpDoc = await _context.MdlpDocuments
                    .FirstOrDefaultAsync(d => d.DocumentId == _currentDocument.Id);

                if (existingMdlpDoc != null)
                {
                    MessageBox.Show($"Документ уже отправлен в МДЛП.\nСтатус: {existingMdlpDoc.StatusText}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    InitializeNewDocument();
                    return;
                }

                // Вызываем МДЛП сервис
                var mdlpResult = await _mdlpService.SendOutgoingAsync(_currentDocument);
                string mdlpStatusText = mdlpResult.Status == "Accepted" ? "Принято" : "Ожидает обработки";

                ShowInfo($"Документ проведен успешно!\nНомер: {_currentDocument.Number}\nID: {documentId}\nМДЛП: {mdlpStatusText} (ID: {mdlpResult.MdlpDocumentId})");

                InitializeNewDocument();
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

            // Обновляем ID текущего документа, если это новый черновик
            if (_currentDocument.Id == 0)
            {
                _currentDocument.Id = documentId;
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

    #endregion

    #region UI Event Handlers

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
                txtQuantity.Text = "1";
                txtQuantity.IsEnabled = false;

                // Загружаем доступные SGTIN для этого товара (статус InCirculation)
                var availableSgtins = _context.MdlpSgtins
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
