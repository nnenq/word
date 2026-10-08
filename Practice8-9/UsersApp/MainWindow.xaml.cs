using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using UsersApp.Data;
using UsersApp.Services;

namespace UsersApp;

/// <summary>Окно регистрации: ввод данных пользователя, проверка и сохранение в базу данных через Entity Framework.</summary>
public partial class MainWindow : Window
{
    readonly UsersContext context = new();
    public string RegisteredEmail { get; private set; } = "";

    public MainWindow()
    {
        InitializeComponent();
        UpdateConditions();
        Closed += (s, e) => context.Dispose();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Список секретных вопросов загружается из таблицы «СекретныйВопрос»
            qwest.ItemsSource = context.СекретныйВопрос.OrderBy(i => i.КодСекретногоВопроса).ToList();
            qwest.DisplayMemberPath = nameof(СекретныйВопрос.СекретныйВопрос1);
            qwest.SelectedValuePath = nameof(СекретныйВопрос.КодСекретногоВопроса);
        }
        catch (SqlException ex)
        {
            dbError.Text = "Нет подключения к базе данных: " + ex.Message;
            SignIn.IsEnabled = false;
        }
        lName.Focus();
    }

    /// <summary>Подсветка выполненных условий пароля при вводе.</summary>
    void UpdateConditions() =>
        conditions.ItemsSource = AuthService.PasswordConditions(passw.Password).Select(c => new
        {
            Text = (c.Ok ? "✓ " : "• ") + c.Text,
            Brush = c.Ok ? Brushes.ForestGreen : Brushes.Gray,
        }).ToList();

    private void Passw_PasswordChanged(object sender, RoutedEventArgs e) => UpdateConditions();

    private void Agree_Changed(object sender, RoutedEventArgs e) => SignIn.IsEnabled = agree.IsChecked == true;

    private void SignIn_Click(object sender, RoutedEventArgs e)
    {
        // Сбрасываем прошлые ошибки
        foreach (var tb in new[] { errФамилия, errИмя, errЭлектроннаяПочта, errПароль, errПовторПароля, errКодовоеСлово, errСекретныйВопрос, errОтвет, errСогласие })
            tb.Text = "";
        dbError.Text = "";

        var input = new RegistrationInput
        {
            Фамилия = lName.Text,
            Имя = name.Text,
            ЭлектроннаяПочта = eMail.Text,
            Пароль = passw.Password,
            ПовторПароля = passw2.Password,
            КодовоеСлово = word.Text,
            КодСекретногоВопроса = qwest.SelectedValue as int?,
            Ответ = otvet.Text,
            СогласенСУсловиями = agree.IsChecked == true,
        };

        try
        {
            var (user, errors) = new AuthService(context).Register(input);
            if (user == null)
            {
                foreach (var (field, text) in errors)
                    if (FindName("err" + field) is TextBlock tb) tb.Text = text;
                return;
            }
            MessageBox.Show($"Пользователь {user.Фамилия} {user.Имя} добавлен", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Information);
            RegisteredEmail = user.ЭлектроннаяПочта;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is SqlException or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            dbError.Text = "Не удалось сохранить пользователя в базе данных: " + (ex.InnerException?.Message ?? ex.Message);
        }
    }
}
