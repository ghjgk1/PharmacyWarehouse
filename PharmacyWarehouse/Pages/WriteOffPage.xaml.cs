using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
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

    private Document _currentDocument;
    private ObservableCollection<DocumentLine> _documentItems;
    private List<Product> _allProducts = new();
    private List<Batch> _availableBatches = new();
    private Batch _selectedBatch;

    private ICollectionView _filteredProductsView;
    private string _productSearchText = string.Empty;
    private ObservableCollection<Product> _allProductsObservable = new();

    public event PropertyChangedEventHandler PropertyChanged;

    private decimal _documentTotal = 0m;
    private int _totalQuantity = 0;

    public WriteOffPage(int? documentId = null)
    {
        InitializeComponent();
        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _batchService = App.ServiceProvider.GetService<BatchService>();

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
            CreatedBy = Environment.UserName,
            CreatedAt = DateTime.Now,
            Number = GenerateDocumentNumber()
        };

        txtDocNumber.Text = _currentDocument.Number;
        dpWriteOffDate.SelectedDate = _currentDocument.Date;
        txtResponsiblePerson.Text = Environment.UserName;
        txtCommission.Text = $"Состав комиссии: {Environment.UserName}";

        _documentItems.Clear();
        UpdateDocumentTotals();
        ResetItemForm();
    }

    private void LoadDraft(int documentId)
    {
        try
        {
            var draft = _documentService.GetById(documentId);
            if (draft == null || draft.Type != DocumentType.WriteOff)
                return;

            _currentDocument = draft;

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

    private void InitializeReasons()
    {
        cmbReason.Items.Clear();
        cmbReason.Items.Add(new ComboBoxItem { Content = "Просроченный срок годности" });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Бой/порча товара" });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Естественная убыль" });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Контрольная закупка" });
        cmbReason.Items.Add(new ComboBoxItem { Content = "Другая причина" });
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

        var selectedProduct = cmbProduct.SelectedItem as Product;
        if (selectedProduct == null) return;

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

            // Сбрасываем форму
            ResetItemForm();

            UpdateDocumentTotals();
            ShowInfo($"Товар '{selectedProduct.Name}' добавлен в акт списания");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка добавления", ex.Message);
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

            // Загружаем доступные партии
            LoadAvailableBatches(product.Id);
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
            _currentDocument.Notes = $"{((ComboBoxItem)cmbReason.SelectedItem)?.Content ?? "Не указана"}" +
                                   (!string.IsNullOrWhiteSpace(txtNotes.Text) ? $"\n{txtNotes.Text}" : "") +
                                   (!string.IsNullOrWhiteSpace(txtCommission.Text) ? $"\n{txtCommission.Text}" : "");
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
                    SellingPrice = item.SellingPrice,
                    Series = item.Series,
                    ExpirationDate = item.ExpirationDate,
                    SourceBatchId = item.SourceBatchId,
                    Notes = item.Notes
                };

                // Если это редактирование существующей строки, сохраняем ID
                if (item.Id != 0)
                {
                    line.Id = item.Id;
                }

                documentLines.Add(line);
            }

            // Сохраняем документ
            int documentId = _documentService.CreateWriteOff(_currentDocument, documentLines);

            // Обновляем ID текущего документа
            _currentDocument.Id = documentId;

            ShowInfo($"Черновик акта списания сохранен. Номер: {_currentDocument.Number}");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка сохранения", ex.Message);
        }
    }

    private void ProcessWriteOff_Click(object sender, RoutedEventArgs e)
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
                _currentDocument.Notes = $"{((ComboBoxItem)cmbReason.SelectedItem)?.Content ?? "Не указана"}" +
                                       (!string.IsNullOrWhiteSpace(txtNotes.Text) ? $"\n{txtNotes.Text}" : "") +
                                       (!string.IsNullOrWhiteSpace(txtCommission.Text) ? $"\n{txtCommission.Text}" : "");
                _currentDocument.Status = DocumentStatus.Processed;
                _currentDocument.Amount = _documentTotal;
                _currentDocument.SignedBy = Environment.UserName;
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
                        Notes = item.Notes
                    };
                    documentLines.Add(line);
                }

                int documentId;
                if (_currentDocument.Id == 0) // Новый документ
                {
                    documentId = _documentService.CreateWriteOff(_currentDocument, documentLines);
                }
                else // Редактирование существующего
                {
                    // Для списания используем тот же метод, что и для расходных накладных
                    documentId = _documentService.CreateWriteOff(_currentDocument, documentLines);
                    _currentDocument.Id = documentId;
                }

                ShowInfo($"Акт списания проведен успешно!\nНомер: {_currentDocument.Number}\nID: {documentId}");
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