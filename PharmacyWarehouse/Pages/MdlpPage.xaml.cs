using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services.Mdlp;
using PharmacyWarehouse.Windows;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PharmacyWarehouse.Pages;

public partial class MdlpPage : Page
{
    private readonly PharmacyWarehouseContext _context;
    private readonly IMdlpService _mdlpService;
    private ObservableCollection<MdlpDocument> _documents;
    private ObservableCollection<MdlpSgtin> _sgtins;
    private MdlpSetting _settings;

    public MdlpPage()
    {
        InitializeComponent();
        _context = new PharmacyWarehouseContext();
        _mdlpService = App.ServiceProvider.GetService<IMdlpService>();
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            // Создаём отдельный контекст для каждой загрузки
            using var context = new PharmacyWarehouseContext();

            var docs = context.MdlpDocuments
                .Include(d => d.Document)
                .OrderByDescending(d => d.SentAt)
                .ToList();

            _settings = context.MdlpSettings.FirstOrDefault() ?? new MdlpSetting
            {
                UseMock = true,
                OrgInn = "7701010000",
                OrgName = "ООО Аптека",
                SimulatedDelaySeconds = 5,
                SimulatedErrorRate = 10,
                MaxRetries = 3
            };

            foreach (var doc in docs)
                doc.MdlpSetting = _settings;

            _documents = new ObservableCollection<MdlpDocument>(docs);
            dgDocuments.ItemsSource = _documents;

            var sgtins = context.MdlpSgtins
                .Include(s => s.Batch)
                .ThenInclude(b => b.Product)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            _sgtins = new ObservableCollection<MdlpSgtin>(sgtins);
            dgSgtins.ItemsSource = _sgtins;

            // Заполняем форму настроек
            chkUseMock.IsChecked = _settings.UseMock;
            txtApiUrl.Text = _settings.ApiUrl;
            txtOrgInn.Text = _settings.OrgInn;
            txtOrgName.Text = _settings.OrgName;
            txtClientId.Text = _settings.ClientId;
            txtSubjectId.Text = _settings.SubjectId;
            sldDelay.Value = _settings.SimulatedDelaySeconds;
            txtDelayLabel.Text = $"Задержка эмуляции (сек): {_settings.SimulatedDelaySeconds}";
            sldErrorRate.Value = _settings.SimulatedErrorRate;
            txtErrorRateLabel.Text = $"Процент случайных ошибок: {_settings.SimulatedErrorRate}%";
            txtMaxRetries.Text = _settings.MaxRetries.ToString();

            sldDelay.ValueChanged += (s, e) =>
                txtDelayLabel.Text = $"Задержка эмуляции (сек): {(int)sldDelay.Value}";
            sldErrorRate.ValueChanged += (s, e) =>
                txtErrorRateLabel.Text = $"Процент случайных ошибок: {(int)sldErrorRate.Value}%";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки данных:\n{ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private async void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.UseMock = chkUseMock.IsChecked ?? true;
            _settings.ApiUrl = txtApiUrl.Text;
            _settings.OrgInn = txtOrgInn.Text;
            _settings.OrgName = txtOrgName.Text;
            _settings.ClientId = txtClientId.Text;
            _settings.SubjectId = txtSubjectId.Text;
            _settings.SimulatedDelaySeconds = (int)sldDelay.Value;
            _settings.SimulatedErrorRate = (int)sldErrorRate.Value;
            _settings.MaxRetries = int.TryParse(txtMaxRetries.Text, out int maxRetries) ? maxRetries : 3;

            if (_settings.Id == 0)
            {
                _context.MdlpSettings.Add(_settings);
            }

            await _context.SaveChangesAsync();
            MessageBox.Show("Настройки сохранены успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void DgDocuments_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgDocuments.SelectedItem is MdlpDocument doc)
        {
            var history = _context.MdlpDocumentHistories
                .Where(h => h.MdlpDocumentId == doc.Id)
                .OrderByDescending(h => h.ChangedAt)
                .ToList();
            dgHistory.ItemsSource = history;

            // Включаем/отключаем кнопку "Повторить отправку"
            btnRetry.IsEnabled = doc.CanRetry;
        }
        else
        {
            dgHistory.ItemsSource = null;
            btnRetry.IsEnabled = false;
        }
    }

    private async void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        if (dgDocuments.SelectedItem is not MdlpDocument doc)
        {
            MessageBox.Show("Выберите документ из списка", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Загрузить оригинальный документ с линиями и продуктами
            var document = await _context.Documents
                .Include(d => d.DocumentLines)
                .ThenInclude(dl => dl.Product)
                .FirstOrDefaultAsync(d => d.Id == doc.DocumentId);

            if (document == null)
            {
                MessageBox.Show("Оригинальный документ не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MdlpDocument result;
            switch (doc.OperationType)
            {
                case "Income":
                    result = await _mdlpService.SendReceiptAsync(document);
                    break;
                case "Outcome":
                    result = await _mdlpService.SendOutgoingAsync(document);
                    break;
                case "WriteOff":
                    result = await _mdlpService.SendWriteOffAsync(document);
                    break;
                default:
                    MessageBox.Show("Неизвестный тип операции", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
            }

            // Обновить документ в базе
            doc.RetryCount++;
            doc.Status = result.Status;
            doc.RequestXml = result.RequestXml;
            doc.ResponseXml = result.ResponseXml;
            doc.SentAt = result.SentAt;
            doc.ProcessedAt = result.ProcessedAt;
            doc.ErrorMessage = result.ErrorMessage;
            doc.ErrorCode = result.ErrorCode;
            doc.ErrorDetails = result.ErrorDetails;

            await _context.SaveChangesAsync();

            MessageBox.Show("Повторная отправка выполнена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка повторной отправки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnViewXml_Click(object sender, RoutedEventArgs e)
    {
        if (dgDocuments.SelectedItem is MdlpDocument doc)
        {
            var window = new XmlViewWindow(doc.RequestXml ?? "", doc.ResponseXml);
            window.ShowDialog();
        }
        else
        {
            MessageBox.Show("Выберите документ из списка", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
