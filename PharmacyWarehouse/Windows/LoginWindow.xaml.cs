using System.Windows;
using PharmacyWarehouse.Services;

namespace PharmacyWarehouse;

public partial class LoginWindow : Window
{
    private readonly AuthService _authService;

    public LoginWindow()
    {
        InitializeComponent();
        _authService = new AuthService();
        _authService.CreateDefaultAdmin();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var login = LoginTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            ShowError("Введите логин и пароль");
            return;
        }

        var user = _authService.Login(login, password);

        if (user == null)
        {
            ShowError("Неверный логин или пароль");
            return;
        }

        // Открываем главное окно
        var mainWindow = new MainWindow();
        mainWindow.Show();
        this.Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}