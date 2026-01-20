using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Pages;
using PharmacyWarehouse.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;


namespace PharmacyWarehouse
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SystemInfoService _systemInfo;
        private readonly DispatcherTimer _statusTimer = new();
            private readonly AuthService _authService;


        public MainWindow()
        {
            InitializeComponent();
            _serviceProvider = App.ServiceProvider;
            LoadUserInfo();
            ApplyRoleRestrictions();
            DataContext = _systemInfo;
            LoadDefaultPage();
            StartStatusUpdater();
        }

        private void LoadUserInfo()
        {
            var user = AuthService.CurrentUser;
            if (user != null)
            {
                UserNameText.Text = user.FullName;
                UserRoleText.Text = user.Role == UserRole.Admin ? "Администратор" : "Фармацевт";
            }
        }

        private void ApplyRoleRestrictions()
        {
            var isAdmin = AuthService.IsAdmin();

            // Справочники
            NavSuppliersBtn.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            NavCategoriesBtn.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            // Документы
            NavWriteOffBtn.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            // Администрирование
            AdminHeader.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            NavManageUsersBtn.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }


        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _authService.Logout();

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void ManageUsers_Click(object sender, RoutedEventArgs e)
        {
            ShowUserManagementDialog();
        }

        private void StartStatusUpdater()
        {
        }
        private void LoadDefaultPage()
        {
            var productsPage = _serviceProvider.GetService<ProductsPage>();
            MainFrame.Navigate(productsPage);
        }

        private void Products_Click(object sender, RoutedEventArgs e)
        {
            var productsPage = _serviceProvider.GetService<ProductsPage>();
            MainFrame.Navigate(productsPage);
        }

        private void Suppliers_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<SuppliersPage>();
            MainFrame.Navigate(page);
        }

        private void Batches_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<BatchesPage>();
            MainFrame.Navigate(page);
        }

        private void Receipt_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<ReceiptPage>();
            MainFrame.Navigate(page);
        }

        private void WriteOff_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<WriteOffPage>();
            MainFrame.Navigate(page);
        }

        private void Stock_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<StockPage>();
            MainFrame.Navigate(page);
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Поиск товаров...")
            {
                SearchTextBox.Text = "";
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Поиск товаров...";
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string searchText = SearchTextBox.Text;

                if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Поиск товаров...")
                {
                    var productsPage = _serviceProvider.GetService<ProductsPage>();
                    if (productsPage != null)
                    {
                        productsPage.SearchTextBox.Text = searchText;
                        MainFrame.Navigate(productsPage);
                    }
                }

                SearchTextBox.Text = "Поиск товаров...";
                Keyboard.ClearFocus();
            }
        }

        private void Sale_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<OutgoingPage>();
            MainFrame.Navigate(page);
        }

        private void Categories_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<CategoriesPage>();
            MainFrame.Navigate(page);
        }

        private void Document_Click(object sender, RoutedEventArgs e)
        {
            var page = _serviceProvider.GetService<DocumentsPage>();
            MainFrame.Navigate(page);

        }

        private void ShowUserManagementDialog()
        {
            var dialog = new Window
            {
                Title = "Управление пользователями",
                Width = 400,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            // Список пользователей
            var listBox = new ListBox
            {
                Height = 200,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var context = BaseDbService.Instance.Context;
            var users = context.Users.ToList();

            foreach (var user in users)
            {
                listBox.Items.Add($"{user.FullName} ({user.Login}) - {user.Role}");
            }

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Список пользователей:",
                Style = (Style)FindResource("H3"),
                Margin = new Thickness(0, 0, 0, 10)
            });
            stackPanel.Children.Add(listBox);

            // Форма создания нового пользователя
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Создать нового пользователя:",
                Style = (Style)FindResource("H3"),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var fullNameBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            var loginBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            var passwordBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 10) };
            var roleCombo = new ComboBox
            {
                ItemsSource = new[] { "Фармацевт", "Администратор" },
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 20)
            };

            stackPanel.Children.Add(new TextBlock { Text = "ФИО:" });
            stackPanel.Children.Add(fullNameBox);
            stackPanel.Children.Add(new TextBlock { Text = "Логин:" });
            stackPanel.Children.Add(loginBox);
            stackPanel.Children.Add(new TextBlock { Text = "Пароль:" });
            stackPanel.Children.Add(passwordBox);
            stackPanel.Children.Add(new TextBlock { Text = "Роль:" });
            stackPanel.Children.Add(roleCombo);

            var createButton = new Button
            {
                Content = "Создать пользователя",
                Style = (Style)FindResource("SuccessButton"),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            createButton.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(fullNameBox.Text) ||
                    string.IsNullOrEmpty(loginBox.Text) ||
                    string.IsNullOrEmpty(passwordBox.Password))
                {
                    MessageBox.Show("Заполните все поля", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var role = roleCombo.SelectedIndex == 0 ? UserRole.Pharmacist : UserRole.Admin;
                var authService = new AuthService();
                var success = authService.CreateUser(
                    fullNameBox.Text.Trim(),
                    loginBox.Text.Trim(),
                    passwordBox.Password,
                    role);

                if (success)
                {
                    MessageBox.Show("Пользователь создан", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("Ошибка при создании пользователя", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            stackPanel.Children.Add(createButton);

            dialog.Content = stackPanel;
            dialog.ShowDialog();
        }
    }
}