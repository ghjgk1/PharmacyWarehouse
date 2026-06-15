using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using System.Windows;
using System.Windows.Controls;

namespace PharmacyWarehouse.Pages;

public partial class NotificationsPage : Page
{
    private readonly NotificationService _notificationService;

    public NotificationsPage()
    {
        InitializeComponent();
        _notificationService = App.ServiceProvider.GetService<NotificationService>()!;
        Loaded += NotificationsPage_Loaded;
    }

    private void NotificationsPage_Loaded(object sender, RoutedEventArgs e)
    {
        var (lowStock, expiring, expired) = _notificationService.GetNotifications();
        dgLowStock.ItemsSource = lowStock;
        dgExpiring.ItemsSource = expiring;
        dgExpired.ItemsSource = expired;
    }

    private void WriteOff_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var product = (sender as Button)?.DataContext as Product;
            if (product == null)
            {
                MessageBox.Show("Товар не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                MessageBox.Show("Главное окно не найдено.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var writeOffPage = App.ServiceProvider.GetService<WriteOffPage>();
            if (writeOffPage == null)
            {
                MessageBox.Show("Страница списания не загружена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            writeOffPage!.PreselectedProductId = product.Id;
            mainWindow?.MainFrame.Navigate(writeOffPage);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateReceipt_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var product = (sender as Button)?.DataContext as Product;
            if (product == null)
            {
                MessageBox.Show("Товар не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                MessageBox.Show("Главное окно не найдено.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var receiptPage = App.ServiceProvider.GetService<ReceiptPage>();
            if (receiptPage == null)
            {
                MessageBox.Show("Страница прихода не загружена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            receiptPage!.PreselectedProductId = product.Id;
            mainWindow?.MainFrame.Navigate(receiptPage);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
