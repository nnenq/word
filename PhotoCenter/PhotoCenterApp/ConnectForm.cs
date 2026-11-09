// Окно подключения к SQL Server. Если базы PhotoCenter нет — предлагает создать её из скрипта PhotoCenter.sql.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PhotoCenterApp
{
    class ConnectForm : Form
    {
        private readonly TextBox _server = new TextBox();
        private static readonly string SettingsFile = Path.Combine(Application.StartupPath, "server.txt");
        public static readonly string ScriptFile = Path.Combine(Application.StartupPath, "PhotoCenter.sql");

        public ConnectForm()
        {
            Text = "Фотоцентр — подключение к SQL Server";
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            ClientSize = new Size(470, 175);

            var title = new Label { Text = "Сервер SQL Server (как в SSMS при подключении):", Location = new Point(15, 15), AutoSize = true };
            _server.Location = new Point(15, 45);
            _server.Width = 440;
            _server.Text = LoadServer();
            var hint = new Label
            {
                Text = "Обычно .\\SQLEXPRESS или (local). Вход — проверка подлинности Windows.",
                Location = new Point(15, 80), AutoSize = true, ForeColor = Color.DimGray
            };
            var connect = new Button { Text = "Подключиться", Location = new Point(225, 120), Size = new Size(125, 36) };
            var exit = new Button { Text = "Выход", Location = new Point(360, 120), Size = new Size(95, 36), DialogResult = DialogResult.Cancel };
            connect.Click += ConnectClick;
            Controls.AddRange(new Control[] { title, _server, hint, connect, exit });
            AcceptButton = connect;
            CancelButton = exit;
        }

        private static string LoadServer()
        {
            try { if (File.Exists(SettingsFile)) return File.ReadAllText(SettingsFile).Trim(); }
            catch { }
            return @".\SQLEXPRESS";
        }

        private void ConnectClick(object sender, EventArgs e)
        {
            Db.Server = _server.Text.Trim();
            Cursor = Cursors.WaitCursor;
            string error = Db.CheckServer();
            Cursor = Cursors.Default;
            if (error != null)
            {
                MessageBox.Show("Не удалось подключиться к серверу «" + Db.Server + "».\n\n" + error +
                                "\n\nПроверьте имя сервера: оно написано в SSMS в «Обозревателе объектов».",
                                "Ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try { File.WriteAllText(SettingsFile, Db.Server); } catch { }

            try
            {
                if (!Db.DatabaseExists())
                {
                    var answer = MessageBox.Show("База данных PhotoCenter на этом сервере не найдена.\nСоздать её с тестовыми данными?",
                        "Создание базы", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer != DialogResult.Yes) return;
                    if (!CreateDatabase(this)) return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Db.ErrorText(ex), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult = DialogResult.OK;
        }

        /// <summary>Создаёт (пересоздаёт) базу из файла PhotoCenter.sql, который лежит рядом с программой.</summary>
        public static bool CreateDatabase(Form owner)
        {
            if (!File.Exists(ScriptFile))
            {
                MessageBox.Show("Не найден файл " + ScriptFile + ".\nОн должен лежать рядом с программой.",
                    "Нет скрипта", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            try
            {
                owner.Cursor = Cursors.WaitCursor;
                Db.RunScript(ScriptFile);
                MessageBox.Show("База PhotoCenter создана. Её видно в SSMS: «Базы данных» → «Обновить».",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при создании базы:\n\n" + Db.ErrorText(ex), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                owner.Cursor = Cursors.Default;
            }
        }
    }
}
