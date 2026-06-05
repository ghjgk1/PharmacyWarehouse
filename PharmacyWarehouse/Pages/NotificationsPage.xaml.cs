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
        var product = (sender as Button)?.DataContext as Product;
        if (product == null) return;

        var mainWindow = Application.Current.MainWindow as MainWindow;
        var writeOffPage = App.ServiceProvider.GetService<WriteOffPage>();
        writeOffPage!.PreselectedProductId = product.Id;
        mainWindow?.MainFrame.Navigate(writeOffPage);
    }

    private void CreateReceipt_Click(object sender, RoutedEventArgs e)
    {
        var product = (sender as Button)?.DataContext as Product;
        if (product == null) return;

        var mainWindow = Application.Current.MainWindow as MainWindow;
        var receiptPage = App.ServiceProvider.GetService<ReceiptPage>();
        receiptPage!.PreselectedProductId = product.Id;
        mainWindow?.MainFrame.Navigate(receiptPage);
    }
}
