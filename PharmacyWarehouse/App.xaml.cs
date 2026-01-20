using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Pages;
using PharmacyWarehouse.Services;
using System.Windows;
using System.Windows.Documents;

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
        }
    }
}
