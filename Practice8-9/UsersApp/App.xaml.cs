using System.Windows;
using System.Windows.Threading;

namespace UsersApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, ev) =>
        {
            MessageBox.Show("Произошла ошибка: " + ev.Exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            ev.Handled = true;
        };
    }
}
