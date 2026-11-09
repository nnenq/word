// Информационная система «Фотоцентр» (экзаменационный билет № 14).
// Windows Forms + Microsoft SQL Server 2014. База данных — PhotoCenter (скрипт PhotoCenter.sql).
using System;
using System.Windows.Forms;

namespace PhotoCenterApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var connect = new ConnectForm())
            {
                if (connect.ShowDialog() != DialogResult.OK) return;
            }
            Application.Run(new MainForm());
        }
    }
}
