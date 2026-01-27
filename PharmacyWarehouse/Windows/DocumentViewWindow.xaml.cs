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
using System.Windows.Media;

namespace PharmacyWarehouse.Windows;

public partial class DocumentViewWindow : Window, INotifyPropertyChanged
{
    private readonly DocumentService _documentService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private Document _document;

    public event PropertyChangedEventHandler PropertyChanged;

    public Document Document
    {
        get => _document;
        set
        {
            _document = value;
            OnPropertyChanged(nameof(Document));
            OnPropertyChanged(nameof(IsIncomingDocument));
            OnPropertyChanged(nameof(IsOutgoingDocument));
            OnPropertyChanged(nameof(IsWriteOffDocument));
            OnPropertyChanged(nameof(IsCorrectionDocument));
            OnPropertyChanged(nameof(ProductsCount));
            OnPropertyChanged(nameof(HasLineNotes));
            OnPropertyChanged(nameof(HasCorrections));
            OnPropertyChanged(nameof(DocumentLinesWithNotes));
            OnPropertyChanged(nameof(WriteOffReasonDescription));
            OnPropertyChanged(nameof(CorrectionTypeDescription));
            OnPropertyChanged(nameof(ShowSeriesColumn));
            OnPropertyChanged(nameof(ShowExpirationColumn));
            OnPropertyChanged(nameof(ShowSellingPriceColumn));
            OnPropertyChanged(nameof(ShowOldValueColumn));
            OnPropertyChanged(nameof(ShowNewValueColumn));
        }
    }

    // Конструктор для перехода с параметром
    public DocumentViewWindow(Document document)
    {
        InitializeComponent();

        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _supplierService = App.ServiceProvider.GetService<SupplierService>();

        LoadDocument(document.Id);
        DataContext = this;
    }

    // Конструктор для перехода по ID
    public DocumentViewWindow(int documentId)
    {
        InitializeComponent();

        _documentService = App.ServiceProvider.GetService<DocumentService>();
        _productService = App.ServiceProvider.GetService<ProductService>();
        _supplierService = App.ServiceProvider.GetService<SupplierService>();

        LoadDocument(documentId);
        DataContext = this;
    }

    private void LoadDocument(int documentId)
    {
        try
        {
            // Загружаем документ со всеми связанными данными
            var document = _documentService.GetDocumentWithDetails(documentId);

            if (document == null)
            {
                MessageBox.Show("Документ не найден", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Document = document;

            // Загружаем данные поставщика для приходной накладной
            if (IsIncomingDocument && Document.SupplierId.HasValue)
            {
                Document.Supplier = _supplierService.GetById(Document.SupplierId.Value);
            }

            // Загружаем оригинальный документ для корректировки
            if (IsCorrectionDocument && Document.OriginalDocumentId.HasValue)
            {
                Document.OriginalDocument = _documentService.GetById(Document.OriginalDocumentId.Value);
            }

            // Загружаем корректировки для основного документа
            if (!IsCorrectionDocument)
            {
                var corrections = _documentService.GetDocumentCorrections(Document.Id);
                if (corrections.Any())
                {
                    Document.Corrections = new ObservableCollection<Document>(corrections);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки документа: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region Свойства для привязки

    // Флаги типов документов
    public bool IsIncomingDocument => Document?.Type == DocumentType.Incoming;
    public bool IsOutgoingDocument => Document?.Type == DocumentType.Outgoing;
    public bool IsWriteOffDocument => Document?.Type == DocumentType.WriteOff;
    public bool IsCorrectionDocument => Document?.Type == DocumentType.Correction;

    // Флаги видимости колонок
    public bool ShowSeriesColumn => IsIncomingDocument || IsCorrectionDocument;
    public bool ShowExpirationColumn => IsIncomingDocument || IsCorrectionDocument;
    public bool ShowSellingPriceColumn => IsOutgoingDocument;
    public bool ShowOldValueColumn => IsCorrectionDocument;
    public bool ShowNewValueColumn => IsCorrectionDocument;

    // Вычисляемые свойства
    public int ProductsCount => Document?.DocumentLines?.Count ?? 0;

    public bool HasLineNotes => Document?.DocumentLines?.Any(l =>
        !string.IsNullOrEmpty(l.Notes) ||
        !string.IsNullOrEmpty(l.CorrectionNotes)) ?? false;

    public bool HasCorrections => Document?.Corrections?.Any() ?? false;

    public IEnumerable<DocumentLineViewModel> DocumentLinesWithNotes
    {
        get
        {
            if (Document?.DocumentLines == null)
                return Enumerable.Empty<DocumentLineViewModel>();

            return Document.DocumentLines
                .Where(l => !string.IsNullOrEmpty(l.Notes) || !string.IsNullOrEmpty(l.CorrectionNotes))
                .Select(l => new DocumentLineViewModel
                {
                    ProductName = l.Product?.Name ?? "Неизвестный товар",
                    Notes = GetLineNotes(l)
                });
        }
    }

    // Описания для отображения
    public string WriteOffReasonDescription
    {
        get
        {
            if (Document?.WriteOffReason == null)
                return string.Empty;

            return Document.WriteOffReason switch
            {
                WriteOffReason.Expired => "Просрочка",
                WriteOffReason.Damaged => "Бой/повреждение",
                WriteOffReason.Defect => "Брак",
                WriteOffReason.Lost => "Потеря",
                WriteOffReason.Recall => "Отзыв Росздравнадзора",
                WriteOffReason.SaleBan => "Запрет на реализацию",
                WriteOffReason.StorageViolation => "Нарушение условий хранения",
                WriteOffReason.Utilization => "Утилизация",
                WriteOffReason.InventoryDifference => "Инвентаризационная разница",
                WriteOffReason.Other => "Иное",
                _ => Document.WriteOffReason.ToString()
            };
        }
    }

    public string CorrectionTypeDescription
    {
        get
        {
            if (Document?.CorrectionType == null)
                return string.Empty;

            return Document.CorrectionType switch
            {
                CorrectionType.Quantity => "Корректировка количества",
                CorrectionType.Price => "Корректировка цены",
                _ => Document.CorrectionType.ToString()
            };
        }
    }

    #endregion

    #region Вспомогательные методы

    private string GetLineNotes(DocumentLine line)
    {
        var notes = new List<string>();

        if (!string.IsNullOrEmpty(line.Notes))
            notes.Add($"Примечание: {line.Notes}");

        if (!string.IsNullOrEmpty(line.CorrectionNotes))
            notes.Add($"Корректировка: {line.CorrectionNotes}");

        return string.Join("\n", notes);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Обработчики событий


    private void DgProducts_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is DocumentLine line)
        {
            // Подсветка строк с корректировками
            if (!string.IsNullOrEmpty(line.OldValue) || !string.IsNullOrEmpty(line.NewValue))
            {
                e.Row.Background = new SolidColorBrush(Color.FromArgb(30, 255, 193, 7)); // Желтый
            }

            // Подсветка просроченных товаров
            else if (line.IsExpiringSoon && IsWriteOffDocument)
            {
                e.Row.Background = new SolidColorBrush(Color.FromArgb(30, 220, 53, 69)); // Красный
            }
        }
    }

    #endregion
}

    #region Вспомогательные классы

    public class DocumentLineViewModel
    {
        public string ProductName { get; set; } = null!;
        public string Notes { get; set; } = null!;
    }

    #endregion
