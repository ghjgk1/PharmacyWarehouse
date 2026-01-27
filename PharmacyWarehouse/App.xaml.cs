using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Pages;
using PharmacyWarehouse.Services;
using PharmacyWarehouse.Services.DocumentGeneration;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PharmacyWarehouse
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // Регистрируем сервисы
            services.AddSingleton<IWordDocumentGenerator, WordDocumentGenerator>();

            services.AddSingleton<AuthService>();
            services.AddSingleton<ProductService>();
            services.AddSingleton<SupplierService>();
            services.AddSingleton<BatchService>();
            services.AddSingleton<CategoryService>();
            services.AddSingleton<DocumentService>();

            // Регистрируем окна/страницы
            services.AddSingleton<MainWindow>();
            services.AddTransient<BatchesPage>();
            services.AddTransient<CategoriesPage>();
            services.AddTransient<DocumentsPage>();
            services.AddTransient<OutgoingPage>();
            services.AddTransient<ProductsPage>();
            services.AddTransient<ReceiptPage>();
            services.AddTransient<SuppliersPage>();
            services.AddTransient<WriteOffPage>();

            ServiceProvider = services.BuildServiceProvider();

            var loginWindow = new LoginWindow();
            loginWindow.Show();

            base.OnStartup(e);

            // Глобальная обработка исключений в UI потоке
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Глобальная обработка исключений в любом потоке
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception, "UI Thread");
            e.Handled = true; // Предотвращаем падение приложения
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex, "Non-UI Thread");
            }
        }

        private void HandleException(Exception ex, string source)
        {
            try
            {
                // Логируем ошибку в файл
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source} Error: {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText("app_errors.log", logMessage);

            }
            catch
            {
                MessageBox.Show("Произошла критическая ошибка.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}