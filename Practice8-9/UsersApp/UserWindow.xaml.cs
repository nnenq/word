using System.Windows;
using System.Windows.Controls;
using UsersApp.Data;

namespace UsersApp;

/// <summary>Окно пользователя после успешного входа.</summary>
public partial class UserWindow : Window
{
    public UserWindow(Пользователь user)
    {
        InitializeComponent();
        hello.Text = $"Здравствуйте, {user.Имя}!";
        (string, string)[] rows =
        {
            ("Код пользователя", user.КодПользователя.ToString()),
            ("Фамилия", user.Фамилия),
            ("Имя", user.Имя),
            ("E-mail", user.ЭлектроннаяПочта),
            ("Кодовое слово", user.КодовоеСлово),
            ("Секретный вопрос", user.СекретныйВопрос?.СекретныйВопрос1 ?? ""),
            ("Дата регистрации", user.ДатаРегистрации.ToString("dd.MM.yyyy HH:mm")),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = rows[i].Item1, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 3, 0, 3) };
            var value = new TextBlock { Text = rows[i].Item2, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            info.Children.Add(label);
            info.Children.Add(value);
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        new LoginWindow().Show();
        Close();
    }
}
