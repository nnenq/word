using System.Globalization;
using System.Text;

namespace CrmSales;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        var ru = new CultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = ru;
        CultureInfo.DefaultThreadCurrentUICulture = ru;
        Thread.CurrentThread.CurrentCulture = ru;
        Thread.CurrentThread.CurrentUICulture = ru;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // для CSV в кодировке Windows-1251

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (s, e) =>
            MessageBox.Show("Произошла ошибка: " + e.Exception.Message + "\n\nДанные сохранены, работу можно продолжить.",
                "CRM", MessageBoxButtons.OK, MessageBoxIcon.Error);

        try
        {
            Db.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось открыть базу данных:\n" + ex.Message +
                            "\n\nСкопируйте последнюю резервную копию из папки Data\\Backups на место файла Data\\crm.db.",
                "CRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // После «Выйти» снова показываем окно входа
        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) break;
            using var main = new MainForm();
            main.ShowDialog();
            if (!main.LoggedOut) break;
        }
    }
}
