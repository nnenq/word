using System.Windows;
using Microsoft.Data.SqlClient;
using UsersApp.Data;
using UsersApp.Services;

namespace UsersApp;

/// <summary>Восстановление пароля по ответу на секретный вопрос.</summary>
public partial class RestoreWindow : Window
{
    public RestoreWindow(string email)
    {
        InitializeComponent();
        eMail.Text = email;
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        error.Text = "";
        try
        {
            using var context = new UsersContext();
            var q = new AuthService(context).QuestionFor(eMail.Text);
            if (q == null)
            {
                error.Text = "Пользователь с таким e-mail не найден.";
                step2.Visibility = Visibility.Collapsed;
                return;
            }
            question.Text = q;
            step2.Visibility = Visibility.Visible;
            otvet.Focus();
        }
        catch (SqlException ex) { error.Text = "Нет подключения к базе данных: " + ex.Message; }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var context = new UsersContext();
            var err = new AuthService(context).ResetPassword(eMail.Text, otvet.Text, passw.Password, passw2.Password);
            if (err != null) { error.Text = err; return; }
            MessageBox.Show("Пароль изменён. Войдите с новым паролем.", "Восстановление пароля", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (SqlException ex) { error.Text = "Нет подключения к базе данных: " + ex.Message; }
    }
}
