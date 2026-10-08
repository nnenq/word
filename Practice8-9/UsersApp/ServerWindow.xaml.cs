using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using UsersApp.Data;

namespace UsersApp;

/// <summary>Настройка имени сервера MS SQL Server.</summary>
public partial class ServerWindow : Window
{
    public ServerWindow()
    {
        InitializeComponent();
        server.Text = ConnectionSettings.Server;
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var conn = new SqlConnection(ConnectionSettings.Build(server.Text.Trim()));
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Пользователь", conn);
            result.Foreground = Brushes.ForestGreen;
            result.Text = $"Подключение успешно. Пользователей в базе: {cmd.ExecuteScalar()}.";
        }
        catch (SqlException ex)
        {
            result.Foreground = Brushes.Firebrick;
            result.Text = "Ошибка подключения: " + ex.Message;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(server.Text)) { result.Text = "Укажите имя сервера."; return; }
        ConnectionSettings.Server = server.Text;
        DialogResult = true;
    }
}
