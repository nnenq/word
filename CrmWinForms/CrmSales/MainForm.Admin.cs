using System.Diagnostics;

namespace CrmSales;

// Вкладка «Администрирование»: пользователи и права, резервные копии, журнал
public partial class MainForm
{
    DataGridView aUsers = null!, aBackups = null!, aLog = null!;
    bool aLoading;

    Control BuildAdmin()
    {
        var inner = new TabControl { Dock = DockStyle.Fill };
        inner.TabPages.Add(Page("Пользователи и права", BuildUsers()));
        inner.TabPages.Add(Page("Резервные копии", BuildBackups()));
        inner.TabPages.Add(Page("База данных", BuildDatabase()));
        inner.TabPages.Add(Page("Журнал действий", BuildLog()));
        inner.SelectedIndexChanged += (s, e) => RefreshAdmin();
        return inner;
    }

    Control BuildUsers()
    {
        aUsers = Ui.Grid();
        aUsers.ReadOnly = false;
        aUsers.Columns.AddRange(Ui.Col("ФИО", 150), Ui.Col("Логин", 90));
        aUsers.Columns.Add(Ui.Combo("Роль", Names.All<Role>(Names.Of), 130));
        aUsers.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Пилотная группа", FillWeight = 70 });
        aUsers.Columns.Add(Ui.Col("Статус", 80));
        aUsers.CellValueChanged += (s, e) =>
        {
            if (aLoading || e.RowIndex < 0) return;
            var u = Db.Data.Users.First(x => x.Id == (string)aUsers.Rows[e.RowIndex].Tag!);
            var cell = aUsers.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (e.ColumnIndex == 2)
            {
                u.Role = Names.Parse<Role>(cell.Value as string, Names.Of);
                if (u.Role != Role.Manager) u.Pilot = false;
                Db.Log($"Роль пользователя {u.Name}: {Names.Of(u.Role)}");
            }
            else if (e.ColumnIndex == 3)
            {
                u.Pilot = u.Role == Role.Manager && cell.Value is true;
                Db.Log($"{u.Name} {(u.Pilot ? "включён в пилотную группу" : "исключён из пилотной группы")}");
            }
            else return;
            Db.Save();
            BeginInvoke(RefreshAll);
        };

        var add = Ui.Btn("Добавить пользователя", (s, e) =>
        {
            using var f = new UserEditForm(null);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshAll();
        }, primary: true);
        var pass = Ui.Btn("Сменить пароль", (s, e) =>
        {
            var u = Db.Data.Users.FirstOrDefault(x => x.Id == Ui.SelectedTag(aUsers));
            if (u == null) return;
            using var f = new UserEditForm(u);
            f.ShowDialog(this);
        });
        var block = Ui.Btn("Заблокировать / разблокировать", (s, e) =>
        {
            var u = Db.Data.Users.FirstOrDefault(x => x.Id == Ui.SelectedTag(aUsers));
            if (u == null) return;
            if (u.Id == Db.Current!.Id) { Ui.Error(this, "Нельзя заблокировать собственную учётную запись."); return; }
            u.Active = !u.Active;
            Db.Log((u.Active ? "Разблокирован " : "Заблокирован ") + u.Name);
            Db.Save();
            RefreshAll();
        });

        var rights = Ui.Grid();
        rights.Columns.AddRange(Ui.Col("Действие", 260), Ui.Col("Менеджер", 70), Ui.Col("Руководитель", 80), Ui.Col("Администратор", 80));
        string[][] matrix =
        {
            new[] { "Свои клиенты, заявки, история", "да", "да", "да" },
            new[] { "Клиенты и заявки всего отдела", "нет", "да", "да" },
            new[] { "Отчёты и критерии успеха", "нет", "да", "да" },
            new[] { "Перенос из Excel, объединение дублей", "нет", "да", "да" },
            new[] { "Управление планом внедрения и рисками", "нет", "да", "да" },
            new[] { "Пользователи, роли, пароли", "нет", "нет", "да" },
            new[] { "Резервные копии и восстановление", "нет", "нет", "да" },
            new[] { "Удаление клиентов", "нет", "нет", "да" },
        };
        foreach (var r in matrix)
        {
            var i = rights.Rows.Add(r);
            for (int c = 1; c < 4; c++) rights.Rows[i].Cells[c].Style.ForeColor = r[c] == "да" ? Ui.Ok : Ui.Muted;
        }

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        split.Controls.Add(Ui.Box("Пользователи (роль и пилотная группа меняются прямо в таблице)", aUsers), 0, 0);
        split.Controls.Add(Ui.Box("Права доступа по ролям", rights), 0, 1);
        return Ui.Stack(1, Ui.Row(add, pass, block), split);
    }

    Control BuildBackups()
    {
        aBackups = Ui.Grid();
        aBackups.Columns.AddRange(Ui.Col("Дата", 100), Ui.Col("Описание", 220), Ui.Col("Кто", 110), Ui.Col("Клиентов", 60, true), Ui.Col("Заявок", 60, true));

        var make = Ui.Btn("Создать копию сейчас", (s, e) =>
        {
            Db.Backup("Ручная копия");
            Db.Log("Создана резервная копия");
            Db.Save();
            RefreshAdmin();
        }, primary: true);
        var restore = Ui.Btn("Восстановить выбранную", (s, e) => RestoreSelected());
        var load = Ui.Btn("Загрузить копию из файла…", (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Резервная копия CRM (*.db)|*.db" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { Db.AddBackupFile(dlg.FileName); RefreshAdmin(); Ui.Info(this, "Копия добавлена в список, её можно восстановить."); }
            catch (Exception ex) { Ui.Error(this, "Не удалось загрузить копию: " + ex.Message); }
        });
        var folder = Ui.Btn("Открыть папку с копиями", (s, e) =>
        {
            Directory.CreateDirectory(Db.BackupDir);
            Process.Start(new ProcessStartInfo { FileName = Db.BackupDir, UseShellExecute = true });
        });
        var reset = Ui.Btn("Сбросить к демо-данным", (s, e) =>
        {
            if (!Ui.Confirm(this, "Все изменения будут заменены демонстрационными данными. Текущее состояние сначала сохранится в резервную копию. Продолжить?")) return;
            Db.ResetDemo();
            plan = null;
            RefreshAll();
        });
        reset.ForeColor = Ui.Bad;

        var hint = Ui.Text($"Копии хранятся в папке {Db.BackupDir}. Копия создаётся автоматически перед каждым переносом данных, объединением дублей, удалением клиента и восстановлением. Хранятся последние 30 копий.", Ui.Small, Ui.Muted);
        hint.MaximumSize = new Size(1150, 0);
        return Ui.Stack(2, Ui.Row(make, restore, load, folder, reset), hint, aBackups);
    }

    void RestoreSelected()
    {
        var path = Ui.SelectedTag(aBackups);
        var b = Db.Backups().FirstOrDefault(x => x.Path == path);
        if (b == null) { Ui.Error(this, "Выберите копию в таблице."); return; }
        if (!Ui.Confirm(this, $"Текущие данные будут заменены копией от {b.Date:dd.MM.yyyy HH:mm} ({b.Clients} клиентов).\nПеред заменой текущее состояние сохранится в новую резервную копию. Продолжить?")) return;
        try
        {
            Db.Restore(b.Path);
            plan = null;
            RefreshAll();
            if (Db.Current != null) Ui.Info(this, $"Данные восстановлены из копии от {b.Date:dd.MM.yyyy HH:mm}.");
        }
        catch (Exception ex) { Ui.Error(this, "Не удалось восстановить: " + ex.Message); }
    }

    Label aDbInfo = null!;
    DataGridView aTables = null!, aQueryResult = null!;
    TextBox aQuery = null!;

    Control BuildDatabase()
    {
        aDbInfo = Ui.Text("", Ui.Font);
        aDbInfo.MaximumSize = new Size(1150, 0);
        aTables = Ui.Grid();
        aTables.Columns.AddRange(Ui.Col("Таблица", 120), Ui.Col("Записей", 60, true), Ui.Col("Что хранит", 260));

        aQuery = new TextBox
        {
            Multiline = true, Height = 70, Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), ScrollBars = ScrollBars.Vertical,
            Text = "SELECT c.Name AS Клиент, u.Name AS Менеджер, COUNT(d.Id) AS Заявок, SUM(d.Amount) AS Сумма\r\n" +
                   "FROM Clients c JOIN Users u ON u.Id = c.OwnerId LEFT JOIN Deals d ON d.ClientId = c.Id\r\n" +
                   "GROUP BY c.Id ORDER BY Сумма DESC",
        };
        aQueryResult = Ui.Grid();
        aQueryResult.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        var run = Ui.Btn("Выполнить запрос", (s, e) =>
        {
            try { aQueryResult.DataSource = Db.RunQuery(aQuery.Text); }
            catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
            { Ui.Error(this, "Ошибка в запросе: " + ex.Message); }
        }, primary: true);
        var examples = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
        (string Title, string Sql)[] samples =
        {
            ("Клиенты по менеджерам и сумма заявок", aQuery.Text),
            ("Воронка: заявки по статусам", "SELECT Status AS Статус, COUNT(*) AS Количество, SUM(Amount) AS Сумма FROM Deals GROUP BY Status"),
            ("История по клиенту", "SELECT i.Date AS Дата, i.Type AS Тип, i.Text AS Содержание FROM Interactions i\r\nJOIN Clients c ON c.Id = i.ClientId WHERE c.Name LIKE '%МедЛайн%' ORDER BY i.Date DESC"),
            ("Пилотная группа", "SELECT Name AS ФИО, Login AS Логин, Role AS Роль FROM Users WHERE Pilot = 1"),
            ("Последние действия пользователей", "SELECT Date AS Дата, User AS Пользователь, Action AS Действие FROM Log ORDER BY Id DESC LIMIT 20"),
            ("Структура таблицы Clients", "SELECT name AS Поле, type AS Тип, pk AS Ключ FROM pragma_table_info('Clients')"),
        };
        examples.Items.AddRange(samples.Select(x => (object)x.Title).ToArray());
        examples.SelectedIndex = 0;
        examples.SelectedIndexChanged += (s, e) => { aQuery.Text = samples[examples.SelectedIndex].Sql; aQueryResult.DataSource = null; };

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.Controls.Add(Ui.Box("Таблицы базы данных", aTables), 0, 0);
        top.Controls.Add(Ui.Box("SQL-запрос к базе (только чтение)",
            Ui.Stack(2, Ui.Row(Ui.Text("Пример:"), examples, run), aQuery, aQueryResult)), 1, 0);

        return Ui.Stack(2, aDbInfo, Ui.Row(Ui.Btn("Открыть папку с базой", (s, e) => OpenDataFolder())), top);
    }

    static readonly Dictionary<string, string> TableNotes = new()
    {
        ["Users"] = "Пользователи, роли, хеши паролей, пилотная группа",
        ["Clients"] = "Единая клиентская база",
        ["Deals"] = "Заявки клиентов (связь с Clients и Users)",
        ["Interactions"] = "История взаимодействия с клиентами",
        ["Stages"] = "9 этапов плана внедрения",
        ["Risks"] = "Реестр рисков",
        ["Feedback"] = "Обратная связь пилотной группы",
        ["Imports"] = "Журнал переносов из Excel со сверкой",
        ["Searches"] = "Замеры времени поиска клиента",
        ["Settings"] = "Настройки и показатели до внедрения",
        ["Log"] = "Журнал действий пользователей",
        ["Meta"] = "Служебная: описание резервной копии",
    };

    static string SqliteVersion()
    {
        using var c = Db.Open(Db.DbPath, readOnly: true);
        return c.ServerVersion;
    }

    void RefreshDatabase()
    {
        var fi = new FileInfo(Db.DbPath);
        aDbInfo.Text = $"СУБД: SQLite {SqliteVersion()}   ·   Файл базы: {fi.FullName}   ·   Размер: {(fi.Exists ? fi.Length / 1024.0 : 0):0.0} КБ   ·   Резервных копий: {Db.Backups().Count}";
        aTables.Rows.Clear();
        foreach (var (table, rows) in Db.TableStats())
            aTables.Rows.Add(table, rows, TableNotes.GetValueOrDefault(table, ""));
    }

    Control BuildLog()
    {
        aLog = Ui.Grid();
        aLog.Columns.AddRange(Ui.Col("Дата", 90), Ui.Col("Пользователь", 110), Ui.Col("Действие", 400));
        return Ui.Stack(0, aLog);
    }

    void RefreshAdmin()
    {
        aLoading = true;
        try
        {
            aUsers.Rows.Clear();
            foreach (var u in Db.Data.Users)
            {
                var i = aUsers.Rows.Add(u.Name, u.Login, Names.Of(u.Role), u.Pilot, u.Active ? "Активен" : "Заблокирован");
                var row = aUsers.Rows[i];
                row.Tag = u.Id;
                row.Cells[0].ReadOnly = row.Cells[1].ReadOnly = row.Cells[4].ReadOnly = true;
                row.Cells[2].ReadOnly = u.Id == Db.Current!.Id; // свою роль менять нельзя
                row.Cells[3].ReadOnly = u.Role != Role.Manager;
                row.Cells[4].Style.ForeColor = u.Active ? Ui.Ok : Ui.Bad;
            }

            aBackups.Rows.Clear();
            foreach (var b in Db.Backups())
            {
                var i = aBackups.Rows.Add(b.Date.ToString("dd.MM.yyyy HH:mm:ss"), b.Label, b.By, b.Clients, b.Deals);
                aBackups.Rows[i].Tag = b.Path;
            }

            RefreshDatabase();

            aLog.Rows.Clear();
            foreach (var l in Db.Data.Log.Take(300)) aLog.Rows.Add(l.Date.ToString("dd.MM.yyyy HH:mm"), l.User, l.Action);
        }
        finally { aLoading = false; }
    }
}
