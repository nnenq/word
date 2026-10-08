using System.Windows;
using Microsoft.Data.SqlClient;
using UsersApp.Data;
using UsersApp.Services;

namespace UsersApp;

/// <summary>Окно авторизации: вход по e-mail и паролю, переход к регистрации и восстановлению пароля.</summary>
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        ShowServer();
        Loaded += (s, e) => eMail.Focus();
    }

    void ShowServer() => serverInfo.Text = $"Сервер БД: {ConnectionSettings.Server}, база {ConnectionSettings.Database}";

    void ShowError(string text)
    {
        error.Text = text;
        error.Visibility = Visibility.Visible;
    }

    private void LogIn_Click(object sender, RoutedEventArgs e)
    {
        error.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(eMail.Text) || passw.Password.Length == 0)
        {
            ShowError("Введите e-mail и пароль.");
            return;
        }
        try
        {
            using var context = new UsersContext();
            var user = new AuthService(context).Login(eMail.Text, passw.Password);
            if (user == null)
            {
                ShowError("Неверный e-mail или пароль.");
                passw.Clear();
                passw.Focus();
                return;
            }
            new UserWindow(user).Show();
            Close();
        }
        catch (SqlException ex)
        {
            ShowError("Нет подключения к базе данных: " + ex.Message + "\nПроверьте имя сервера (ссылка «изменить» внизу) и что база UsersDB создана скриптом CreateDatabase.sql.");
        }
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        var w = new MainWindow { Owner = this };
        if (w.ShowDialog() == true)
        {
            eMail.Text = w.RegisteredEmail;
            passw.Focus();
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e) =>
        new RestoreWindow(eMail.Text) { Owner = this }.ShowDialog();

    private void Server_Click(object sender, RoutedEventArgs e)
    {
        var w = new ServerWindow { Owner = this };
        if (w.ShowDialog() == true) ShowServer();
    }
}
