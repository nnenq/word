namespace CrmSales;

/// <summary>Главное окно. Набор вкладок зависит от роли пользователя (права доступа).</summary>
public partial class MainForm : Form
{
    public bool LoggedOut { get; private set; }

    readonly TabControl tabs = new() { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
    readonly Dictionary<TabPage, Action> refreshers = new();

    public MainForm()
    {
        Ui.Style(this);
        var me = Db.Current!;
        Text = $"CRM · Отдел продаж — {me.Name} ({Names.Of(me.Role)})";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1280, 820);
        MinimumSize = new Size(1000, 640);

        var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White, Padding = new Padding(14, 8, 14, 8) };
        var logout = Ui.Btn("Выйти", (s, e) => Logout());
        logout.Dock = DockStyle.Right;
        var who = new Label
        {
            Text = $"{me.Name} · {Names.Of(me.Role)}{(me.Pilot ? " · пилотная группа" : "")}", AutoSize = false, Dock = DockStyle.Right,
            Width = 420, TextAlign = ContentAlignment.MiddleRight, ForeColor = Ui.Muted, Padding = new Padding(0, 0, 12, 0),
        };
        var brand = new Label { Text = $"CRM · Продажи   {Db.Data.Settings.Company}", Font = Ui.H2, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ui.Accent };
        header.Controls.Add(brand);
        header.Controls.Add(who);
        header.Controls.Add(logout);

        AddTab("Клиенты", BuildClients(), RefreshClients);
        AddTab("Заявки", BuildDeals(), RefreshDeals);
        if (Db.CanManage) AddTab("Отчёты", BuildReports(), RefreshReports);
        AddTab("Внедрение", BuildImpl(), RefreshImpl);
        if (Db.CanManage) AddTab("Перенос данных", BuildImport(), RefreshImport);
        if (Db.IsAdmin) AddTab("Администрирование", BuildAdmin(), RefreshAdmin);

        tabs.SelectedIndexChanged += (s, e) => { if (tabs.SelectedTab != null) refreshers[tabs.SelectedTab](); };
        Controls.Add(tabs);
        Controls.Add(header);
        Controls.Add(BuildMenu());
        Shown += (s, e) => RefreshAll();
    }

    MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { BackColor = Color.White, Font = Ui.Font };
        var file = new ToolStripMenuItem("Файл");
        file.DropDownItems.Add(new ToolStripMenuItem("Выгрузить клиентов и заявки в Excel…", null, (s, e) => ExportExcel(), Keys.Control | Keys.E));
        if (Db.CanManage)
            file.DropDownItems.Add("Загрузить клиентов из Excel…", null, (s, e) => GoTo("Перенос данных"));
        file.DropDownItems.Add("Открыть папку с базой данных", null, (s, e) => OpenDataFolder());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Выйти из учётной записи", null, (s, e) => Logout());
        var help = new ToolStripMenuItem("Справка");
        help.DropDownItems.Add("О программе", null, (s, e) => Ui.Info(this,
            "CRM «Отдел продаж»\n\nПрактическая работа «Внедрение CRM-системы».\nC# WinForms, .NET 8, база данных SQLite, Excel через ClosedXML.\n\n" +
            "Файл базы данных:\n" + Db.DbPath));
        menu.Items.Add(file);
        menu.Items.Add(help);
        MainMenuStrip = menu;
        return menu;
    }

    void OpenDataFolder()
    {
        Directory.CreateDirectory(Db.DataDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Db.DataDir, UseShellExecute = true });
    }

    void AddTab(string title, Control content, Action refresh)
    {
        var page = new TabPage(title) { Padding = new Padding(12), BackColor = Ui.Back };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
        refreshers[page] = refresh;
    }

    TabPage? FindTab(string title) => tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text == title);

    void GoTo(string title)
    {
        var p = FindTab(title);
        if (p != null) tabs.SelectedTab = p;
    }

    public void RefreshAll()
    {
        if (Db.Current == null)
        {
            Ui.Info(this, "Ваша учётная запись отсутствует в восстановленных данных. Войдите заново.");
            LoggedOut = true;
            Close();
            return;
        }
        foreach (var r in refreshers.Values) r();
    }

    void Logout()
    {
        Db.Log("Выход из системы");
        Db.Save();
        Db.Current = null;
        LoggedOut = true;
        Close();
    }

    void ExportExcel()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Книга Excel (*.xlsx)|*.xlsx", FileName = $"crm_klienty_{DateTime.Now:yyyy-MM-dd}.xlsx",
            Title = "Выгрузка клиентской базы в Excel",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var clients = Db.VisibleClients().ToList();
            Importer.ExportExcel(dlg.FileName, clients, Db.VisibleDeals());
            Db.Log($"Выгрузка клиентской базы в Excel ({clients.Count} записей)");
            Db.Save();
            Ui.Info(this, $"Выгружено клиентов: {clients.Count}.\nФайл: {dlg.FileName}");
        }
        catch (IOException ex)
        {
            Ui.Error(this, "Не удалось сохранить файл. Возможно, он открыт в Excel — закройте его и повторите.\n\n" + ex.Message);
        }
    }

    void OpenClient(string? id)
    {
        if (id == null) return;
        if (searchWatch != null)
        {
            Metrics.RecordSearch(searchWatch.Elapsed.TotalSeconds);
            searchWatch = null;
            Db.Save();
        }
        using var f = new ClientCardForm(id);
        f.ShowDialog(this);
        RefreshAll();
    }
}
