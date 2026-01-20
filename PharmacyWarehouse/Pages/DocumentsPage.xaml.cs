using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Pages;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PharmacyWarehouse.Pages
{
    public partial class DocumentsPage : Page
    {
        private readonly DocumentService _documentService;
        private ObservableCollection<DocumentViewModel> _documents;
        private ICollectionView _documentsView;

        private bool _showZeroAmount = true;
        public DocumentsPage()
        {
            InitializeComponent();
            _documentService = App.ServiceProvider.GetService<DocumentService>();
            Loaded += DocumentsPage_Loaded;
        }

        private void DocumentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDocuments();
            UpdateStatistics();
        }

        #region Загрузка данных

        private void LoadDocuments()
        {
            _documents = new ObservableCollection<DocumentViewModel>();

            // Загружаем все документы
            var allDocuments = _documentService.Documents
                .OrderByDescending(d => d.Date)
                .ThenByDescending(d => d.Id);

            foreach (var doc in allDocuments)
            {
                _documents.Add(new DocumentViewModel(doc));
            }

            // Настраиваем представление с группировкой
            _documentsView = CollectionViewSource.GetDefaultView(_documents);
            _documentsView.GroupDescriptions.Add(new PropertyGroupDescription("GroupKey"));

            dgDocuments.ItemsSource = _documentsView;
            UpdateFilterInfo();
        }

        private void UpdateStatistics()
        {
            if (_documents == null) return;

            txtTotalCount.Text = $"Всего: {_documents.Count}";
            txtArrivalCount.Text = $"Приход: {_documents.Count(d => d.Type == DocumentType.Incoming)}";
            txtOutgoingCount.Text = $"Расход: {_documents.Count(d => d.Type == DocumentType.Outgoing)}";
            txtWriteOffCount.Text = $"Списание: {_documents.Count(d => d.Type == DocumentType.WriteOff)}";

            txtDraftCount.Text = $"Черновики: {_documents.Count(d => d.Status == DocumentStatus.Draft)}";
            txtProcessedCount.Text = $"Проведенные: {_documents.Count(d => d.Status == DocumentStatus.Processed)}";
            txtBlockedCount.Text = $"Заблокированные: {_documents.Count(d => d.Status == DocumentStatus.Blocked)}";
        }

        #endregion

        #region Создание документов

        private void CreateArrival_Click(object sender, RoutedEventArgs e)
        {
            var page = new ReceiptPage();
            NavigationService.Navigate(page);
        }

        private void CreateOutgoing_Click(object sender, RoutedEventArgs e)
        {
            var page = new OutgoingPage();
            NavigationService.Navigate(page);
        }

        private void CreateWriteOff_Click(object sender, RoutedEventArgs e)
        {
            // Здесь нужно создать страницу для списания
            MessageBox.Show("Создание акта списания - функция в разработке");
        }

        #endregion
        private void ChkShowZeroAmount_Checked(object sender, RoutedEventArgs e)
        {
            _showZeroAmount = true;
            ApplyDocumentFilter();
            UpdateStatistics();
        }

        private void ChkShowZeroAmount_Unchecked(object sender, RoutedEventArgs e)
        {
            _showZeroAmount = false;
            ApplyDocumentFilter();
            UpdateStatistics();
        }


        #region Управление документами

        private void EditDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is DocumentViewModel selectedDoc)
            {
                // Проверяем, можно ли редактировать (только черновики)
                if (selectedDoc.Status != DocumentStatus.Draft)
                {
                    MessageBox.Show("Редактировать можно только черновики",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Открываем соответствующую страницу редактирования
                switch (selectedDoc.Type)
                {
                    case DocumentType.Incoming:
                        var receiptPage = new ReceiptPage(selectedDoc.Id);
                        NavigationService.Navigate(receiptPage);
                        break;
                    case DocumentType.Outgoing:
                        var outgoingPage = new OutgoingPage(selectedDoc.Id);
                        NavigationService.Navigate(outgoingPage);
                        break;
                    case DocumentType.WriteOff:
                        var writeOffPage = new OutgoingPage(selectedDoc.Id); // Используем ту же страницу
                        NavigationService.Navigate(writeOffPage);
                        break;
                }
            }
            else
            {
                MessageBox.Show("Выберите документ для редактирования",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ViewDocumentDetails_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is not DocumentViewModel selectedDocVM)
            {
                MessageBox.Show("Выберите документ для просмотра",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ViewDocumentDetails(selectedDocVM.Id);
        }

        private void ViewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is DocumentViewModel selectedDocVM)
            {
                ViewDocumentDetails(selectedDocVM.Id);
            }
        }

        private void ViewDocumentDetails(int documentId)
        {
            try
            {
                // Загружаем полный документ из базы
                var document = _documentService.GetDocumentWithDetails(documentId);

                if (document == null)
                {
                    MessageBox.Show("Документ не найден",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Создаем страницу просмотра документа
                var documentViewPage = new DocumentViewWindow(document);
                var result = documentViewPage.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProcessDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is DocumentViewModel selectedDoc)
            {
                var result = MessageBox.Show(
                    $"Провести документ №{selectedDoc.Number}?\n" +
                    $"После проведения редактирование будет невозможно.",
                    "Проведение документа",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var document = _documentService.GetById(selectedDoc.Id);
                        if (document != null)
                        {
                            document.Status = DocumentStatus.Processed;
                            document.SignedAt = DateTime.Now;
                            document.SignedBy = Environment.UserName;

                            _documentService.Commit();
                            LoadDocuments(); // Обновляем список

                            MessageBox.Show($"Документ №{selectedDoc.Number} проведен",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка проведения документа: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите документ для проведения",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BlockDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is DocumentViewModel selectedDoc)
            {
                var result = MessageBox.Show(
                    $"Заблокировать документ №{selectedDoc.Number}?\n" +
                    $"После блокировки операции по документу будут невозможны.",
                    "Блокировка документа",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var document = _documentService.GetById(selectedDoc.Id);
                        if (document != null)
                        {
                            document.Status = DocumentStatus.Blocked;
                            _documentService.Commit();
                            LoadDocuments(); // Обновляем список

                            MessageBox.Show($"Документ №{selectedDoc.Number} заблокирован",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка блокировки документа: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region Фильтрация и поиск

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbDocumentType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void DateRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            cmbDocumentType.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
            dpDateFrom.SelectedDate = null;
            dpDateTo.SelectedDate = null;

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_documentsView == null) return;

            _documentsView.Filter = obj =>
            {
                if (obj is not DocumentViewModel doc) return false;

                // Поиск по тексту
                if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    var searchText = SearchTextBox.Text.ToLower();
                    if (!doc.Number.ToLower().Contains(searchText) &&
                        !(doc.CustomerName?.ToLower().Contains(searchText) ?? false) &&
                        !(doc.Notes?.ToLower().Contains(searchText) ?? false))
                        return false;
                }

                // Фильтр по типу документа
                if (cmbDocumentType.SelectedIndex > 0)
                {
                    var selectedType = (cmbDocumentType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                    if (selectedType != doc.Type.ToString())
                        return false;
                }

                if (!_showZeroAmount)
                {
                    int hiddenZeroAmount = _documents.Count(d => d.Amount == 0 && !ShouldShowDocument(d));
                    if (hiddenZeroAmount > 0)
                    {
                        txtFilterInfo.Text += $" (скрыто с нулевой суммой: {hiddenZeroAmount})";
                    }
                }

                // Фильтр по статусу
                if (cmbStatus.SelectedIndex > 0)
                {
                    var selectedStatus = (cmbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                    if (selectedStatus != doc.Status.ToString())
                        return false;
                }

                // Фильтр по дате
                if (dpDateFrom.SelectedDate.HasValue && doc.Date < dpDateFrom.SelectedDate.Value)
                    return false;

                if (dpDateTo.SelectedDate.HasValue && doc.Date > dpDateTo.SelectedDate.Value)
                    return false;

                // Фильтр по нулевой сумме
                if (!chkShowZeroAmount.IsChecked.GetValueOrDefault() && doc.Amount == 0)
                    return false;

                return true;
            };

            UpdateFilterInfo();
        }
        private bool ShouldShowDocument(DocumentViewModel doc)
        {
            if (!_showZeroAmount && doc.Amount == 0)
            {
                return false; 
            }

            return true;
        }

        private void ApplyDocumentFilter()
        {
            if (_documentsView == null) return;

            _documentsView.Filter = obj =>
            {
                return obj is DocumentViewModel doc && ShouldShowDocument(doc);
            };

            UpdateFilterInfo();
        }

        private void UpdateFilterInfo()
        {
            if (_documentsView == null) return;

            int count = _documentsView.Cast<object>().Count();
            txtFilterInfo.Text = $"Найдено: {count}";
        }

        private void ChkGroupByType_Checked(object sender, RoutedEventArgs e)
        {
            if (_documentsView != null)
            {
                _documentsView.GroupDescriptions.Clear();
                _documentsView.GroupDescriptions.Add(new PropertyGroupDescription("GroupKey"));
            }
        }

        private void ChkGroupByType_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_documentsView != null)
            {
                _documentsView.GroupDescriptions.Clear();
            }
        }

        #endregion

        #region Пагинация

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Простая реализация пагинации
            if (cmbPageSize.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int pageSize))
            {
                // Здесь можно добавить логику пагинации
            }
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentPage.Text = "1";
            // Здесь можно добавить логику перехода на первую страницу
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtCurrentPage.Text, out int currentPage) && currentPage > 1)
            {
                txtCurrentPage.Text = (currentPage - 1).ToString();
                // Здесь можно добавить логику перехода на предыдущую страницу
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtCurrentPage.Text, out int currentPage))
            {
                txtCurrentPage.Text = (currentPage + 1).ToString();
                // Здесь можно добавить логику перехода на следующую страницу
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            // Здесь можно добавить логику перехода на последнюю страницу
        }

        private void TxtCurrentPage_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Валидация номера страницы
            if (int.TryParse(txtCurrentPage.Text, out int page) && page < 1)
            {
                txtCurrentPage.Text = "1";
            }
        }

        #endregion

        #region Контекстное меню

        private void PrintDocument_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Печать документа - функция в разработке");
        }

        private void CopyNumber_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is DocumentViewModel selectedDoc)
            {
                Clipboard.SetText(selectedDoc.Number);
                MessageBox.Show($"Номер документа скопирован: {selectedDoc.Number}",
                              "Копирование",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        private void DeleteDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dgDocuments.SelectedItem is DocumentViewModel selectedDoc)
            {
                // Удалять можно только черновики
                if (selectedDoc.Status != DocumentStatus.Draft)
                {
                    MessageBox.Show("Удалить можно только черновики",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Удалить документ №{selectedDoc.Number}?\n" +
                    $"Это действие нельзя отменить.",
                    "Удаление документа",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var document = _documentService.GetById(selectedDoc.Id);
                        if (document != null)
                        {
                            // Удаляем связанные строки документа
                            var lines = _documentService.Documents
                                .Where(d => d.Id == selectedDoc.Id)
                                .SelectMany(d => d.DocumentLines)
                                .ToList();

                            foreach (var line in lines)
                            {
                                _documentService.Documents.FirstOrDefault(d => d.Id == selectedDoc.Id)
                                    ?.DocumentLines.Remove(line);
                            }

                            // Удаляем сам документ
                            _documentService.Documents.Remove(document);
                            _documentService.Commit();

                            LoadDocuments(); // Обновляем список

                            MessageBox.Show($"Документ №{selectedDoc.Number} удален",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления документа: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region Вспомогательные методы

        private void RefreshDocuments_Click(object sender, RoutedEventArgs e)
        {
            LoadDocuments();
            UpdateStatistics();
            MessageBox.Show("Список документов обновлен",
                "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DgDocuments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обновляем доступность кнопок в зависимости от выбранного документа
            var isDocumentSelected = dgDocuments.SelectedItem != null;
            btnEdit.IsEnabled = isDocumentSelected;
            btnProcess.IsEnabled = isDocumentSelected;
            btnBlock.IsEnabled = isDocumentSelected;
        }

        #endregion

        private void CreateCorrection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем, выбран ли документ
                if (dgDocuments.SelectedItem is not DocumentViewModel selectedDocVM)
                {
                    MessageBox.Show("Выберите документ для корректировки",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Загружаем полный документ из базы
                var document = _documentService.GetDocumentWithCorrections(selectedDocVM.Id);

                if (document == null)
                {
                    MessageBox.Show("Документ не найден",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (document.Status == DocumentStatus.Blocked)
                {
                    MessageBox.Show("Невозможно создать корректировку для заблокированного документа",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (document.DocumentLines == null || !document.DocumentLines.Any())
                {
                    MessageBox.Show("В документе нет товаров для корректировки",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (document.Type != DocumentType.Incoming)
                {
                    
                    var hasBatches = document.DocumentLines
                        .Any(l => l.CreatedBatchId.HasValue || l.SourceBatchId.HasValue);

                    if (!hasBatches)
                    {
                        MessageBox.Show("Для корректировки расходного документа необходимо наличие партий",
                            "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                
                var correctionWindow = new CorrectionDocumentWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                var result = correctionWindow.ShowDialog();

                if (result == true)
                {
                    
                    LoadDocuments();
                    UpdateStatistics();

                    var corrections = _documents
                        .Where(d => d.Type == DocumentType.Correction)
                        .OrderByDescending(d => d.CreatedAt)
                        .ToList();

                    if (corrections.Any())
                    {
                        var latestCorrection = corrections.First();
                        var viewItem = _documents.FirstOrDefault(d => d.Id == latestCorrection.Id);

                        if (viewItem != null)
                        {
                            dgDocuments.SelectedItem = viewItem;
                            dgDocuments.ScrollIntoView(viewItem);
                        }
                    }

                    MessageBox.Show("Корректировочный документ создан успешно",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании корректировки: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #region ViewModel для отображения документов

    #region ViewModel для отображения документов

    public class DocumentViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private string _correctionReason;
        private string _supplierName;
        private string _customerName;
        private string _notes;
        private decimal _amount;
        private string _writeOffReason;
        private int _linesCount;
        private string _typeDisplayName;
        private string _typeIcon;
        private string _groupKey;
        private string _statusDisplayName;
        private string _baseDocumentInfo;
        private string _correctionTypeDisplayName;

        public DocumentViewModel(Document document)
        {
            _document = document;
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            _correctionReason = _document.CorrectionReason ?? "-";
            _supplierName = _document.Supplier?.Name ?? "-";
            _customerName = _document.CustomerName ?? "-";
            _notes = _document.Notes ?? "-";
            _amount = _document.Amount ?? 0;
            _writeOffReason = ExtractWriteOffReason(_document.Notes);
            _linesCount = _document.DocumentLines?.Count ?? 0;
            _typeDisplayName = GetTypeDisplayName();
            _typeIcon = GetTypeIcon();
            _groupKey = GetGroupKey();
            _statusDisplayName = GetStatusDisplayName();
            _baseDocumentInfo = GetBaseDocumentInfo();
            _correctionTypeDisplayName = GetCorrectionTypeDisplayName();
        }

        public int Id
        {
            get => _document.Id;
            set
            {
                if (_document.Id != value)
                {
                    _document.Id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Number
        {
            get => _document.Number;
            set
            {
                if (_document.Number != value)
                {
                    _document.Number = value;
                    OnPropertyChanged(nameof(Number));
                }
            }
        }

        public DateTime Date
        {
            get => _document.Date;
            set
            {
                if (_document.Date != value)
                {
                    _document.Date = value;
                    OnPropertyChanged(nameof(Date));
                }
            }
        }

        public DateTime CreatedAt
        {
            get => _document.CreatedAt;
            set
            {
                if (_document.CreatedAt != value)
                {
                    _document.CreatedAt = value;
                    OnPropertyChanged(nameof(CreatedAt));
                }
            }
        }

        public string CreatedBy
        {
            get => _document.CreatedBy;
            set
            {
                if (_document.CreatedBy != value)
                {
                    _document.CreatedBy = value;
                    OnPropertyChanged(nameof(CreatedBy));
                }
            }
        }

        public DocumentType Type
        {
            get => _document.Type;
            set
            {
                if (_document.Type != value)
                {
                    _document.Type = value;
                    OnPropertyChanged(nameof(Type));
                    // Обновляем зависимые свойства
                    TypeDisplayName = GetTypeDisplayName();
                    TypeIcon = GetTypeIcon();
                    GroupKey = GetGroupKey();
                }
            }
        }

        public DocumentStatus Status
        {
            get => _document.Status;
            set
            {
                if (_document.Status != value)
                {
                    _document.Status = value;
                    OnPropertyChanged(nameof(Status));
                    StatusDisplayName = GetStatusDisplayName();
                }
            }
        }

        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (_customerName != value)
                {
                    _customerName = value;
                    OnPropertyChanged(nameof(CustomerName));
                    _document.CustomerName = value == "-" ? null : value;
                }
            }
        }

        public string Notes
        {
            get => _notes;
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged(nameof(Notes));
                    _document.Notes = value == "-" ? null : value;
                    WriteOffReason = ExtractWriteOffReason(value);
                }
            }
        }

        public decimal Amount
        {
            get => _amount;
            set
            {
                if (_amount != value)
                {
                    _amount = value;
                    OnPropertyChanged(nameof(Amount));
                    _document.Amount = value;
                }
            }
        }

        public string SupplierName
        {
            get => _supplierName;
            set
            {
                if (_supplierName != value)
                {
                    _supplierName = value;
                    OnPropertyChanged(nameof(SupplierName));

                    if (_document.Supplier != null && value != "-")
                    {
                        _document.Supplier.Name = value;
                    }
                }
            }
        }

        public string WriteOffReason
        {
            get => _writeOffReason;
            private set
            {
                if (_writeOffReason != value)
                {
                    _writeOffReason = value;
                    OnPropertyChanged(nameof(WriteOffReason));
                }
            }
        }

        public string CorrectionReason
        {
            get => _correctionReason;
            set
            {
                if (_correctionReason != value)
                {
                    _correctionReason = value;
                    OnPropertyChanged(nameof(CorrectionReason));
                    _document.CorrectionReason = value == "-" ? null : value;
                }
            }
        }

        public int LinesCount
        {
            get => _linesCount;
            set
            {
                if (_linesCount != value)
                {
                    _linesCount = value;
                    OnPropertyChanged(nameof(LinesCount));
                }
            }
        }

        public int? OriginalDocumentId
        {
            get => _document.OriginalDocumentId;
            set
            {
                if (_document.OriginalDocumentId != value)
                {
                    _document.OriginalDocumentId = value;
                    OnPropertyChanged(nameof(OriginalDocumentId));
                    BaseDocumentInfo = GetBaseDocumentInfo();
                }
            }
        }

        public string CorrectionTypeDisplayName
        {
            get => _correctionTypeDisplayName;
            set
            {
                if (_correctionTypeDisplayName != value)
                {
                    _correctionTypeDisplayName = value;
                    OnPropertyChanged(nameof(CorrectionTypeDisplayName));
                }
            }
        }

        public string TypeDisplayName
        {
            get => _typeDisplayName;
            set
            {
                if (_typeDisplayName != value)
                {
                    _typeDisplayName = value;
                    OnPropertyChanged(nameof(TypeDisplayName));
                }
            }
        }

        public string TypeIcon
        {
            get => _typeIcon;
            set
            {
                if (_typeIcon != value)
                {
                    _typeIcon = value;
                    OnPropertyChanged(nameof(TypeIcon));
                }
            }
        }

        public string GroupKey
        {
            get => _groupKey;
            set
            {
                if (_groupKey != value)
                {
                    _groupKey = value;
                    OnPropertyChanged(nameof(GroupKey));
                }
            }
        }

        public string BaseDocumentInfo
        {
            get => _baseDocumentInfo;
            set
            {
                if (_baseDocumentInfo != value)
                {
                    _baseDocumentInfo = value;
                    OnPropertyChanged(nameof(BaseDocumentInfo));
                }
            }
        }

        public string StatusDisplayName
        {
            get => _statusDisplayName;
            set
            {
                if (_statusDisplayName != value)
                {
                    _statusDisplayName = value;
                    OnPropertyChanged(nameof(StatusDisplayName));
                }
            }
        }

        public CorrectionType? CorrectionType
        {
            get => _document.CorrectionType;
            set
            {
                if (_document.CorrectionType != value)
                {
                    _document.CorrectionType = value;
                    OnPropertyChanged(nameof(CorrectionType));
                    CorrectionTypeDisplayName = GetCorrectionTypeDisplayName();
                }
            }
        }

        private string GetTypeDisplayName()
        {
            return _document.Type switch
            {
                DocumentType.Incoming => "Приходная накладная",
                DocumentType.Outgoing => "Расходная накладная",
                DocumentType.WriteOff => "Акт списания",
                DocumentType.Correction => "Корректировка",
                _ => _document.Type.ToString()
            };
        }

        private string GetTypeIcon()
        {
            return _document.Type switch
            {
                DocumentType.Incoming => "📥",
                DocumentType.Outgoing => "📤",
                DocumentType.WriteOff => "🗑️",
                DocumentType.Correction => "✏️",
                _ => "📄"
            };
        }

        private string GetGroupKey()
        {
            return _document.Type switch
            {
                DocumentType.Incoming => "📥 Приходные накладные",
                DocumentType.Outgoing => "📤 Расходные накладные",
                DocumentType.WriteOff => "🗑️ Акты списания",
                DocumentType.Correction => "✏️ Корректировки",
                _ => _document.Type.ToString()
            };
        }

        private string GetStatusDisplayName()
        {
            return _document.Status switch
            {
                DocumentStatus.Draft => "Черновик",
                DocumentStatus.Processed => "Проведен",
                DocumentStatus.Blocked => "Заблокирован",
                _ => _document.Status.ToString()
            };
        }

        private string GetBaseDocumentInfo()
        {
            if (_document.Type != DocumentType.Correction || !_document.OriginalDocumentId.HasValue)
                return "-";

            return $"Основание: {_document.OriginalDocument?.Number ?? _document.OriginalDocumentId.ToString()}";
        }

        private string GetCorrectionTypeDisplayName()
        {
            return _document.CorrectionType.HasValue
                ? _document.CorrectionType.Value switch
                {
                    Models.CorrectionType.Quantity => "Корректировка количества",
                    Models.CorrectionType.Price => "Корректировка цены",
                    Models.CorrectionType.ExpirationDate => "Корректировка срока годности",
                    Models.CorrectionType.Series => "Корректировка серии",
                    Models.CorrectionType.Product => "Корректировка товара",
                    _ => "Корректировка"
                }
                : "-";
        }

        private string ExtractWriteOffReason(string notes)
        {
            if (string.IsNullOrEmpty(notes))
                return "Списание";

            var lines = notes.Split('\n');
            return lines.Length > 0 ? lines[0].Trim() : "Списание";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion

    #endregion
}