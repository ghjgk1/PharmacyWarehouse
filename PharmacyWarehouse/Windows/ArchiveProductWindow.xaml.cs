using PharmacyWarehouse.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace PharmacyWarehouse.Windows;

public partial class ArchiveProductWindow : Window, INotifyPropertyChanged
{
    public ArchiveInfo Result { get; private set; }
    private Product _product;
    private bool _hasStock;
    private bool _isCriticalReason = false;

    private bool _isWindowLoaded = false;

    public event PropertyChangedEventHandler PropertyChanged;

    public ArchiveProductWindow(Product product)
    {
        _isWindowLoaded = false;
        InitializeComponent();
        _product = product;
        _hasStock = product.CurrentStock > 0;
        DataContext = this;

        txtComment.Text = $"Архивация товара '{product.Name}' (ID: {product.Id})";

        if (!_hasStock)
        {
            actionPanel.Visibility = Visibility.Collapsed;
        }

        UpdateReasonDescription();

        _isWindowLoaded = true;
    }

    public string ProductName => _product.Name;
    public decimal CurrentStock => _product.CurrentStock;
    public bool HasStock => _hasStock;

    private void CmbReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbReason.SelectedItem is ComboBoxItem selectedItem)
        {
            string tag = selectedItem.Tag as string;
            _isCriticalReason = tag == "Critical";

            UpdateReasonDescription();
            UpdateActionPanel();
        }
    }

    private void UpdateReasonDescription()
    {
        if (!_isWindowLoaded) return;

        if (cmbReason.SelectedItem is ComboBoxItem selectedItem)
        {
            string description = selectedItem.Content.ToString() switch
            {
                "Снят с производства" => "Производитель прекратил выпуск товара",
                "Изменение ассортимента аптеки" => "Исключен из текущего ассортимента аптеки",
                "Устаревшая форма выпуска" => "Выпускается в новой форме/дозировке",
                "Замена на аналог" => "Заменен аналогичным товаром",
                "Отзыв Росздравнадзора" => "Отзыв с рынка по решению регулятора. Требуется срочное списание!",
                "Запрет на реализацию" => "Запрещено к продаже по решению контролирующих органов",
                "Просроченный товар" => "Истек срок годности. Товар не подлежит реализации!",
                "Брак/Повреждение" => "Товар поврежден! Не подлежит реализации.",
                "Нарушение условий хранения" => "Нарушен температурный режим или условия хранения",
                "Другая причина" => "Укажите подробности в комментарии",
                _ => ""
            };

            txtReasonDescription.Text = description;
        }
    }

    private void UpdateActionPanel()
    {
        if (!_hasStock) return;

        if (_isCriticalReason)
        {
            rbWriteOff.IsChecked = true;
            rbSellOut.IsEnabled = false;
            criticalWarning.Visibility = Visibility.Visible;

            var selectedReason = (cmbReason.SelectedItem as ComboBoxItem)?.Content.ToString();
            ShowCriticalWarning(selectedReason);
        }
        else
        {
            rbSellOut.IsEnabled = true;
            rbSellOut.IsChecked = true; 
            criticalWarning.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowCriticalWarning(string reason)
    {
        string warningText = reason switch
        {
            "Отзыв Росздравнадзора" => "Товар отозван с рынка! Немедленно списать остатки.",
            "Запрет на реализацию" => "Продажа запрещена! Остатки подлежат списанию.",
            "Просроченный товар" => "Срок годности истек! Товар опасен для здоровья.",
            "Брак/Повреждение" => "Товар поврежден! Не подлежит реализации.",
            "Нарушение условий хранения" => "Нарушены условия хранения! Товар может быть опасен.",
            _ => "Требуется обязательное списание остатков!"
        };

        var warningBlock = (criticalWarning.Child as StackPanel)?.Children
            .OfType<TextBlock>().FirstOrDefault();

        if (warningBlock != null)
        {
            warningBlock.Text = $"⚠ {warningText}";
        }
    }

    private WriteOffReason ConvertToWriteOffReason(string archiveReason)
    {
        return archiveReason switch
        {
            "Отзыв Росздравнадзора" => WriteOffReason.Recall,
            "Запрет на реализацию" => WriteOffReason.SaleBan,
            "Просроченный товар" => WriteOffReason.Expired,
            "Брак/Повреждение" => WriteOffReason.Damaged,
            "Нарушение условий хранения" => WriteOffReason.StorageViolation,
            _ => WriteOffReason.Other
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (cmbReason.SelectedItem is ComboBoxItem selectedItem)
        {
            var reason = selectedItem.Content.ToString();
            var tag = selectedItem.Tag as string;

            bool requiresWriteOff = _isCriticalReason || (_hasStock && rbWriteOff.IsChecked == true);
            bool isSellOutMode = _hasStock && !requiresWriteOff;

            WriteOffReason? writeOffReason = requiresWriteOff
               ? ConvertToWriteOffReason(reason)
               : (WriteOffReason?)null;

            Result = new ArchiveInfo
            {
                Reason = reason,
                Comment = txtComment.Text,
                IsCritical = _isCriticalReason,
                RequiresWriteOff = requiresWriteOff,
                IsSellOutMode = isSellOutMode,
                WriteOffReason = writeOffReason
            };

            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
public class ArchiveInfo
{
    public string Reason { get; set; }
    public string Comment { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresWriteOff { get; set; }
    public bool IsSellOutMode { get; set; }
    public WriteOffReason? WriteOffReason { get; set; }
}