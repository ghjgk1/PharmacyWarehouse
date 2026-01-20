using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace PharmacyWarehouse.Windows
{
    public partial class CorrectionDocumentWindow : Window
    {
        private readonly DocumentService _documentService;
        private readonly ProductService _productService;
        private readonly Document _baseDocument;
        private ObservableCollection<CorrectionProductViewModel> _products;

        public CorrectionDocumentWindow(Document baseDocument)
        {
            InitializeComponent();
            _documentService = App.ServiceProvider.GetService<DocumentService>();
            _productService = App.ServiceProvider.GetService<ProductService>();
            _baseDocument = baseDocument;
            _products = new ObservableCollection<CorrectionProductViewModel>();

            Loaded += CorrectionDocumentWindow_Loaded;
        }

        private void CorrectionDocumentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeDocumentInfo();
                LoadProductsFromBaseDocument();

                dpDocDate.SelectedDate = DateTime.Now;
                cmbCorrectionType.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void InitializeDocumentInfo()
        {
            txtBaseDocument.Text = $"{GetDocumentTypeName(_baseDocument.Type)} №{_baseDocument.Number}";
            txtBaseDate.Text = _baseDocument.Date.ToString("dd.MM.yyyy");
            txtBaseAmount.Text = $"{_baseDocument.Amount:N2} руб.";
            txtDocNumber.Text = GenerateCorrectionNumber();
        }

        private void LoadProductsFromBaseDocument()
        {
            _products.Clear();
            itemsProducts.ItemsSource = _products;

            var document = _documentService.GetDocumentWithCorrections(_baseDocument.Id);

            if (document?.DocumentLines == null || !document.DocumentLines.Any())
            {
                MessageBox.Show("В документе нет товаров для корректировки",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var line in document.DocumentLines)
            {
                var batch = line.CreatedBatch ?? line.SourceBatch;
                var product = _productService.GetById(line.ProductId);

                if (product == null)
                    continue;

                var productVM = new CorrectionProductViewModel
                {
                    Id = line.Id,
                    DocumentLineId = line.Id,
                    ProductId = line.ProductId,
                    ProductName = product.Name,
                    Unit = product.UnitOfMeasure ?? "шт.",

                    // Текущие значения из документа
                    CurrentQuantity = line.Quantity,
                    CurrentPrice = line.UnitPrice,
                    CurrentExpirationDate = batch?.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(1)),

                    // Новые значения (по умолчанию такие же как текущие)
                    NewQuantity = line.Quantity,
                    NewPrice = line.UnitPrice,
                    NewExpirationDate = batch?.ExpirationDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(1)),

                    BatchId = batch?.Id,
                    Product = product,
                    SourceBatch = batch
                };

                _products.Add(productVM);
            }

            UpdateStatistics();
        }

        private string GenerateCorrectionNumber()
        {
            var date = DateTime.Now;
            var count = _documentService.GetDocumentsCount(date.Month, date.Year);
            return $"КОР-{date:MMyy}-{count + 1:D4}";
        }

        private string GetDocumentTypeName(DocumentType type)
        {
            return type switch
            {
                DocumentType.Incoming => "Приходная накладная",
                DocumentType.Outgoing => "Расходная накладная",
                DocumentType.WriteOff => "Акт списания",
                DocumentType.Correction => "Корректировка",
                _ => "Документ"
            };
        }

        private void UpdateStatistics()
        {
            if (_products == null) return;

            int totalItems = _products.Count;
            int changedItems = _products.Count(p => p.HasChanges);

            txtProductCount.Text = $"({totalItems})";
            txtItemsCount.Text = $"{totalItems}";
            txtChangedItemsCount.Text = $"{changedItems}";

            decimal totalCorrection = _products.Sum(p => p.CorrectionAmount);
            txtTotalCorrection.Text = $"{totalCorrection:N2} руб.";

            // Обновляем цвет
            if (totalCorrection > 0)
                txtTotalCorrection.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            else if (totalCorrection < 0)
                txtTotalCorrection.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            else
                txtTotalCorrection.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
        }

        private void CmbCorrectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCorrectionType.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                string tag = selectedItem.Tag.ToString();
                var correctionType = tag switch
                {
                    "Quantity" => CorrectionType.Quantity,
                    "Price" => CorrectionType.Price,
                    "ExpirationDate" => CorrectionType.ExpirationDate,
                    _ => CorrectionType.Quantity
                };

                // Обновляем тип коррекции для всех товаров
                foreach (var product in _products)
                {
                    product.SetCorrectionType(correctionType);
                }

                UpdateStatistics();
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is CorrectionProductViewModel productVM)
            {
                _products.Remove(productVM);
                UpdateStatistics();
            }
        }

        private void BtnResetItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is CorrectionProductViewModel productVM)
            {
                productVM.ResetChanges();
                UpdateStatistics();
            }
        }

        private void BtnRecalculate_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatistics();
        }

        private void BtnResetChanges_Click(object sender, RoutedEventArgs e)
        {
            foreach (var product in _products)
            {
                product.ResetChanges();
            }
            UpdateStatistics();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSaveProcess_Click(object sender, RoutedEventArgs e)
        {
            SaveDocument();
        }

        private bool ValidateDocument()
        {
            if (string.IsNullOrWhiteSpace(txtDocumentReason.Text))
            {
                MessageBox.Show("Укажите причину корректировки",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            bool hasChanges = _products.Any(p => p.HasChanges);

            if (!hasChanges)
            {
                MessageBox.Show("Нет изменений для корректировки",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private void SaveDocument()
        {
            if (!ValidateDocument())
                return;

            try
            {
                var quantityCorrections = _products
                    .Where(p => p.HasChanges)
                    .Select(p => new QuantityCorrection
                    {
                        DocumentLineId = p.DocumentLineId,
                        NewQuantity = p.NewQuantity
                    })
                    .ToList();

                if (quantityCorrections.Any())
                {
                    _documentService.CreateQuantityCorrection(
                        _baseDocument.Id,
                        txtDocumentReason.Text,
                        quantityCorrections
                    );

                    MessageBox.Show($"Корректировка проведена успешно!\nНомер: {txtDocNumber.Text}",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Нет изменений для сохранения",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class CorrectionProductViewModel : INotifyPropertyChanged
    {
        private CorrectionType _currentCorrectionType = CorrectionType.Quantity;

        public int Id { get; set; }
        public int DocumentLineId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string ProductCode { get; set; } = null!;
        public string Unit { get; set; } = null!;

        // Текущие значения
        public int CurrentQuantity { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateOnly CurrentExpirationDate { get; set; }

        // Новые значения
        private int _newQuantity;
        public int NewQuantity
        {
            get => _newQuantity;
            set
            {
                if (_newQuantity != value)
                {
                    _newQuantity = value;
                    OnPropertyChanged(nameof(NewQuantity));
                    OnPropertyChanged(nameof(HasChanges));
                    CalculateCorrections();
                }
            }
        }

        private decimal _newPrice;
        public decimal NewPrice
        {
            get => _newPrice;
            set
            {
                if (_newPrice != value)
                {
                    _newPrice = value;
                    OnPropertyChanged(nameof(NewPrice));
                    OnPropertyChanged(nameof(HasChanges));
                    CalculateCorrections();
                }
            }
        }

        private DateOnly _newExpirationDate;
        public DateOnly NewExpirationDate
        {
            get => _newExpirationDate;
            set
            {
                if (_newExpirationDate != value)
                {
                    _newExpirationDate = value;
                    OnPropertyChanged(nameof(NewExpirationDate));
                    OnPropertyChanged(nameof(NewExpirationDateDateTime));
                    OnPropertyChanged(nameof(HasChanges));
                }
            }
        }

        public DateTime? NewExpirationDateDateTime
        {
            get => NewExpirationDate.ToDateTime(TimeOnly.MinValue);
            set
            {
                if (value.HasValue)
                {
                    NewExpirationDate = DateOnly.FromDateTime(value.Value);
                }
            }
        }

        // Вычисляемые свойства
        private decimal _correctionAmount;
        public decimal CorrectionAmount
        {
            get => _correctionAmount;
            private set
            {
                if (_correctionAmount != value)
                {
                    _correctionAmount = value;
                    OnPropertyChanged(nameof(CorrectionAmount));
                }
            }
        }

        // Флаги видимости для XAML
        public bool IsQuantityCorrection => _currentCorrectionType == CorrectionType.Quantity;
        public bool IsPriceCorrection => _currentCorrectionType == CorrectionType.Price;
        public bool IsExpirationDateCorrection => _currentCorrectionType == CorrectionType.ExpirationDate;
        public bool ShowCorrectionAmount => IsQuantityCorrection || IsPriceCorrection;

        // Отображаемое текущее значение
        public string CurrentValueDisplay => _currentCorrectionType switch
        {
            CorrectionType.Quantity => $"{CurrentQuantity:N0} {Unit}",
            CorrectionType.Price => $"{CurrentPrice:N2} руб.",
            CorrectionType.ExpirationDate => CurrentExpirationDate.ToString("dd.MM.yyyy"),
            _ => ""
        };

        // Есть ли изменения
        public bool HasChanges => _currentCorrectionType switch
        {
            CorrectionType.Quantity => NewQuantity != CurrentQuantity,
            CorrectionType.Price => NewPrice != CurrentPrice,
            CorrectionType.ExpirationDate => NewExpirationDate != CurrentExpirationDate,
            _ => false
        };

        // Ссылки
        public int? BatchId { get; set; }
        public Product? Product { get; set; }
        public Batch? SourceBatch { get; set; }

        public void SetCorrectionType(CorrectionType correctionType)
        {
            _currentCorrectionType = correctionType;

            OnPropertyChanged(nameof(IsQuantityCorrection));
            OnPropertyChanged(nameof(IsPriceCorrection));
            OnPropertyChanged(nameof(IsExpirationDateCorrection));
            OnPropertyChanged(nameof(ShowCorrectionAmount));
            OnPropertyChanged(nameof(CurrentValueDisplay));
            OnPropertyChanged(nameof(HasChanges));

            CalculateCorrections();
        }

        public void CalculateCorrections()
        {
            switch (_currentCorrectionType)
            {
                case CorrectionType.Quantity:
                    int quantityDiff = NewQuantity - CurrentQuantity;
                    CorrectionAmount = quantityDiff * CurrentPrice;
                    break;

                case CorrectionType.Price:
                    decimal priceDiff = NewPrice - CurrentPrice;
                    CorrectionAmount = priceDiff * CurrentQuantity;
                    break;

                case CorrectionType.ExpirationDate:
                    CorrectionAmount = 0;
                    break;
            }
        }

        public void ResetChanges()
        {
            switch (_currentCorrectionType)
            {
                case CorrectionType.Quantity:
                    NewQuantity = CurrentQuantity;
                    break;

                case CorrectionType.Price:
                    NewPrice = CurrentPrice;
                    break;

                case CorrectionType.ExpirationDate:
                    NewExpirationDate = CurrentExpirationDate;
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}