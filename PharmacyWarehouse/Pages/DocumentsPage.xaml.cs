using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Pages;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.DocumentGeneration;
using PharmacyWarehouse.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
        private readonly AuthService _authService;
        private readonly IWordDocumentGenerator _wordGenerator;
        private string _currentUser = String.Empty;

        private List<DocumentViewModel> _allDocuments = new();
        private List<DocumentViewModel> _filteredDocuments = new();
        private List<DocumentViewModel> _currentPageDocuments = new();
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;
        private bool _isPageLoaded = false;


        private bool _showZeroAmount = true;
        public DocumentsPage()
        {
            InitializeComponent();
            _documentService = App.ServiceProvider.GetService<DocumentService>();
            _authService = App.ServiceProvider.GetService<AuthService>();
            _wordGenerator = App.ServiceProvider.GetService<IWordDocumentGenerator>();
            _currentUser = AuthService.CurrentUser?.FullName ?? "Неизвестный пользователь";

            Loaded += DocumentsPage_Loaded;
        }

        private void DocumentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isPageLoaded = false;
            LoadDocuments();
            _isPageLoaded = true; 
            ApplyFilters();

            var isAdmin = AuthService.IsAdmin();
            btnCorrection.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnBlock.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Загрузка данных

        private void LoadDocuments()
        {
            try
            {
                _documents = new ObservableCollection<DocumentViewModel>();
                _allDocuments = new List<DocumentViewModel>();

                var allDocuments = _documentService.Documents
                    .OrderByDescending(d => d.Date)
                    .ThenByDescending(d => d.Id);

                foreach (var doc in allDocuments)
                {
                    var viewModel = new DocumentViewModel(doc);
                    _allDocuments.Add(viewModel);
                    _documents.Add(viewModel);
                }

                _filteredDocuments = _allDocuments.ToList();
                _currentPage = 1;

                UpdateDataGridWithFilters();
                UpdateStatistics();
                UpdatePageInfo();
                UpdateFilterInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки документов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void UpdateDataGridWithoutFilters()
        {
            try
            {
                if (dgDocuments == null) return;

                var startIndex = (_currentPage - 1) * _pageSize;
                _currentPageDocuments = _filteredDocuments
                    .Skip(startIndex)
                    .Take(_pageSize > 0 ? _pageSize : _filteredDocuments.Count)
                    .ToList();

                var groupedCollection = new ListCollectionView(_currentPageDocuments);

                if (chkGroupByType != null && chkGroupByType.IsChecked == true)
                {
                    groupedCollection.GroupDescriptions.Add(new PropertyGroupDescription("GroupKey"));
                }

                dgDocuments.ItemsSource = groupedCollection;
                dgDocuments.UpdateLayout();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления таблицы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDataGridWithFilters()
        {
            try
            {
                ApplyDocumentFilters(); 
                UpdateDataGridWithoutFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления таблицы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDataGrid()
        {
            UpdateDataGridWithFilters();
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

        private void UpdatePageInfo()
        {
            if (!_isPageLoaded) return;

            try
            {
                var filteredCount = _filteredDocuments.Count;
                _totalPages = _pageSize > 0 ? (int)Math.Ceiling(filteredCount / (double)_pageSize) : 1;
                if (_totalPages == 0) _totalPages = 1;

                // Проверяем корректность текущей страницы
                if (_currentPage > _totalPages)
                {
                    _currentPage = _totalPages;
                }

                txtTotalPages.Text = _totalPages.ToString();
                txtCurrentPage.Text = _currentPage.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в UpdatePageInfo: {ex.Message}");
            }
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
                        var writeOffPage = new WriteOffPage(selectedDoc.Id); 
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
                if (selectedDoc.Status != DocumentStatus.Draft)
                {
                    MessageBox.Show("Провести можно только черновики",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

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
                            document.SignedBy = _currentUser;

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
            try
            {
                _currentPage = 1;
                ApplyDocumentFilters();
                UpdateDataGrid();
                UpdateStatistics();
                UpdatePageInfo();
                UpdateFilterInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения фильтров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyDocumentFilters()
        {
            try
            {
                var filtered = _allDocuments.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchTextBox?.Text))
                {
                    var searchText = SearchTextBox.Text.ToLower();
                    filtered = filtered.Where(doc =>
                        doc.Number.ToLower().Contains(searchText) ||
                        (doc.CustomerName?.ToLower().Contains(searchText) ?? false) ||
                        (doc.Notes?.ToLower().Contains(searchText) ?? false));
                }

                if (cmbDocumentType?.SelectedIndex > 0 && cmbDocumentType.SelectedItem is ComboBoxItem typeItem)
                {
                    var selectedType = typeItem.Tag?.ToString();
                    if (!string.IsNullOrEmpty(selectedType))
                    {
                        filtered = filtered.Where(doc => doc.Type.ToString() == selectedType);
                    }
                }

                if (cmbStatus?.SelectedIndex > 0 && cmbStatus.SelectedItem is ComboBoxItem statusItem)
                {
                    var selectedStatus = statusItem.Tag?.ToString();
                    if (!string.IsNullOrEmpty(selectedStatus))
                    {
                        filtered = filtered.Where(doc => doc.Status.ToString() == selectedStatus);
                    }
                }

                if (dpDateFrom?.SelectedDate.HasValue == true)
                {
                    filtered = filtered.Where(doc => doc.Date >= dpDateFrom.SelectedDate.Value);
                }

                if (dpDateTo?.SelectedDate.HasValue == true)
                {
                    filtered = filtered.Where(doc => doc.Date <= dpDateTo.SelectedDate.Value);
                }

                if (!_showZeroAmount)
                {
                    filtered = filtered.Where(doc => doc.Amount != 0);
                }

                _filteredDocuments = filtered.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в ApplyDocumentFilters: {ex.Message}");
                _filteredDocuments = _allDocuments.ToList();
            }
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
            if (_filteredDocuments == null) return;

            int count = _filteredDocuments.Count;
            if (txtFilterInfo != null)
                txtFilterInfo.Text = $"Найдено: {count}";
        }

        private void ChkGroupByType_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDataGrid();
        }

        private void ChkGroupByType_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateDataGrid();
        }

        #endregion

        #region Пагинация

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isPageLoaded) return;

            if (cmbPageSize.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string tag &&
                int.TryParse(tag, out int newSize))
            {
                _pageSize = newSize;
                _currentPage = 1;
                UpdateDataGrid();
                UpdatePageInfo();
            }
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            UpdateDataGrid();
            UpdatePageInfo();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdateDataGrid();
                UpdatePageInfo();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdateDataGrid();
                UpdatePageInfo();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = _totalPages;
            UpdateDataGrid();
            UpdatePageInfo();
        }

        private void TxtCurrentPage_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isPageLoaded) return;

            if (int.TryParse(txtCurrentPage.Text, out int page) &&
                page >= 1 && page <= _totalPages &&
                page != _currentPage)
            {
                _currentPage = page;
                UpdateDataGrid();
                UpdatePageInfo();
            }
        }

        #endregion

        #region Контекстное меню

        private void PrintDocument_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dgDocuments.SelectedItem is not DocumentViewModel selectedDocVM)
                {
                    MessageBox.Show("Выберите документ для печати",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Диалог сохранения файла
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Документ Word (*.docx)|*.docx|PDF файл (*.pdf)|*.pdf",
                    FileName = $"{selectedDocVM.Number}_{selectedDocVM.Date:yyyyMMdd}",
                    DefaultExt = ".docx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var document = _documentService.GetById(selectedDocVM.Id);
                    if (document == null)
                    {
                        MessageBox.Show("Документ не найден в базе данных",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Генерируем соответствующий тип документа
                    switch (document.Type)
                    {
                        case DocumentType.Incoming:
                            _wordGenerator.GenerateArrivalInvoice(document, saveDialog.FileName);
                            break;
                        case DocumentType.Outgoing:
                            _wordGenerator.GenerateOutgoingInvoice(document, saveDialog.FileName);
                            break;
                        case DocumentType.WriteOff:
                            _wordGenerator.GenerateWriteOffAct(document, saveDialog.FileName);
                            break;
                        case DocumentType.Correction:
                            _wordGenerator.GenerateCorrectionAct(document, saveDialog.FileName);
                            break;
                        default:
                            MessageBox.Show("Тип документа не поддерживается для печати",
                                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            break;
                    }

                    // Открываем документ после создания
                    if (File.Exists(saveDialog.FileName))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = saveDialog.FileName,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

    public class DocumentViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly string _correctionReason;
        private readonly string _supplierName;
        private readonly string _customerName;
        private readonly string _notes;
        private readonly decimal _amount;
        private readonly string _writeOffReason;
        private readonly int _linesCount;
        private readonly string _typeDisplayName;
        private readonly string _typeIcon;
        private readonly string _groupKey;
        private readonly string _statusDisplayName;
        private readonly string _baseDocumentInfo;
        private readonly string _correctionTypeDisplayName;
        public string Number => _document.Number;

        public DocumentViewModel(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

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

        // Все свойства только для чтения
        public int Id => _document.Id;
        public DateTime Date => _document.Date;
        public DateTime CreatedAt => _document.CreatedAt;
        public string CreatedBy => _document.CreatedBy;
        public DocumentType Type => _document.Type;
        public DocumentStatus Status => _document.Status;
        public string CustomerName => _customerName;
        public string Notes => _notes;
        public decimal Amount => _amount;
        public string SupplierName => _supplierName;
        public string WriteOffReason => _writeOffReason;
        public string CorrectionReason => _correctionReason;
        public int LinesCount => _linesCount;
        public int? OriginalDocumentId => _document.OriginalDocumentId;
        public string CorrectionTypeDisplayName => _correctionTypeDisplayName;
        public string TypeDisplayName => _typeDisplayName;
        public string TypeIcon => _typeIcon;
        public string GroupKey => _groupKey;
        public string BaseDocumentInfo => _baseDocumentInfo;
        public string StatusDisplayName => _statusDisplayName;
        public CorrectionType? CorrectionType => _document.CorrectionType;

        // Вспомогательные методы для вычисляемых свойств
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

        // Метод для обновления ViewModel при изменении документа
        public DocumentViewModel UpdateFromDocument(Document updatedDocument)
        {
            if (updatedDocument == null)
                return this;

            // Создаем новый экземпляр с обновленными данными
            return new DocumentViewModel(updatedDocument);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    #endregion
}

