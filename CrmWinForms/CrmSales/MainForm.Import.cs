namespace CrmSales;

// Вкладка «Перенос данных»: импорт из Excel/CSV со сверкой, объединение дублей
public partial class MainForm
{
    Importer.Plan? plan;
    Label mFile = null!, mPreview = null!;
    DataGridView mMap = null!, mHistory = null!, mDups = null!;
    Button mRun = null!, mCancel = null!;
    List<List<Client>> dupGroups = new();
    bool mLoading;

    Control BuildImport()
    {
        var pick = Ui.Btn("Выбрать файл Excel или CSV…", (s, e) => PickFile(), primary: true);
        var demo = Ui.Btn("Загрузить пример файла", (s, e) => SetPlan(Importer.Demo()));
        var intro = Ui.Text("Загрузите файл, в котором отдел вёл клиентов. Система сама определит столбцы, найдёт совпадения с уже внесёнными клиентами и объединит их, " +
                            "а после переноса сверит количество строк. Перед переносом автоматически создаётся резервная копия.\n" +
                            "Чтобы изменить клиентов через Excel: выгрузите базу (меню «Файл»), исправьте данные в файле, не трогая столбец ID, и загрузите файл здесь.", Ui.Small, Ui.Muted);
        intro.MaximumSize = new Size(1150, 0);

        mFile = Ui.Text("Файл не выбран", Ui.Bold);
        mMap = Ui.Grid();
        mMap.ReadOnly = false;
        mMap.Columns.AddRange(Ui.Col("Столбец в файле", 120), Ui.Col("Пример значения", 200));
        mMap.Columns.Add(Ui.Combo("Поле в CRM", Importer.Fields.Select(f => f.Title).ToArray(), 120));
        mMap.CellValueChanged += (s, e) =>
        {
            if (mLoading || plan == null || e.RowIndex < 0 || e.ColumnIndex != 2) return;
            plan.Map[e.RowIndex] = Importer.FieldKey(mMap.Rows[e.RowIndex].Cells[2].Value as string);
            BeginInvoke(UpdatePreview);
        };
        mPreview = Ui.Note("", Ui.Ink, Color.White);
        mRun = Ui.Btn("Перенести в CRM", (s, e) => RunImport(), primary: true);
        mCancel = Ui.Btn("Отменить", (s, e) => SetPlan(null));

        var mapBox = Ui.Box("Сопоставление столбцов", Ui.Stack(1, mFile, mMap, mPreview, Ui.Row(mRun, mCancel)));

        mHistory = Ui.Grid();
        mHistory.Columns.AddRange(Ui.Col("Дата", 100), Ui.Col("Файл", 170), Ui.Col("Строк", 50, true), Ui.Col("Новых", 50, true),
            Ui.Col("Объединено", 70, true), Ui.Col("Изменено", 60, true), Ui.Col("Пустых", 50, true), Ui.Col("Сверка", 90));

        mDups = Ui.Grid();
        mDups.Columns.AddRange(Ui.Col("Группа", 40, true), Ui.Col("Запись", 70), Ui.Col("Клиент", 150), Ui.Col("Телефон / email / ИНН", 200), Ui.Col("Менеджер", 100));
        var mergeOne = Ui.Btn("Объединить выбранную группу", (s, e) => MergeSelected());
        var mergeAll = Ui.Btn("Объединить все", (s, e) => MergeAll());
        var dupsBox = Ui.Box("Дубли в базе (совпадение телефона, email, ИНН или названия)",
            Ui.Stack(0, mDups, Ui.Row(mergeOne, mergeAll)));

        var bottom = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.Controls.Add(Ui.Box("Журнал переносов и сверка", mHistory), 0, 0);
        bottom.Controls.Add(dupsBox, 1, 0);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        split.Controls.Add(mapBox, 0, 0);
        split.Controls.Add(bottom, 0, 1);

        return Ui.Stack(2, Ui.Row(pick, demo), intro, split);
    }

    void PickFile()
    {
        using var dlg = new OpenFileDialog { Filter = "Excel и CSV (*.xlsx;*.csv)|*.xlsx;*.xlsm;*.csv|Все файлы|*.*", Title = "Файл с клиентской базой" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { SetPlan(Importer.Load(dlg.FileName)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
        {
            Ui.Error(this, "Не удалось прочитать файл: " + ex.Message + (ex is IOException ? "\nЕсли файл открыт в Excel, закройте его и повторите." : ""));
        }
    }

    void SetPlan(Importer.Plan? p)
    {
        plan = p;
        RefreshImport();
    }

    void UpdatePreview()
    {
        if (plan == null)
        {
            mPreview.Text = "Выберите файл или загрузите пример.";
            mPreview.ForeColor = Ui.Muted; mPreview.BackColor = Color.White;
            mRun.Enabled = mCancel.Enabled = false;
            return;
        }
        mCancel.Enabled = true;
        if (!plan.HasName)
        {
            mPreview.Text = "Укажите, какой столбец содержит название компании или ФИО.";
            mPreview.ForeColor = Ui.Bad; mPreview.BackColor = Ui.BadSoft;
            mRun.Enabled = false;
            return;
        }
        var p = Importer.Analyze(plan);
        var text = $"Строк в файле: {plan.Rows.Count}.  Новых клиентов: {p.Created}.  Совпадают с базой и будут объединены: {p.Merged}.  " +
                   $"Изменены в Excel: {p.Updated}.  Без изменений: {p.Unchanged}.  Строк без названия (не переносятся): {p.Empty}.";
        if (p.Changes.Count > 0)
            text += "\n\nБудут изменены:\n" + string.Join("\n", p.Changes.Take(8).Select(c => "• " + c)) +
                    (p.Changes.Count > 8 ? $"\n…и ещё {p.Changes.Count - 8}" : "");
        mPreview.Text = text;
        mPreview.ForeColor = Ui.Ink; mPreview.BackColor = Ui.OkSoft;
        mRun.Enabled = true;
        mRun.Text = p.Updated > 0 && p.Created + p.Merged == 0 ? "Применить изменения" : "Перенести в CRM";
    }

    void RunImport()
    {
        if (plan == null) return;
        var rec = Importer.Run(plan);
        plan = null;
        RefreshAll();
        Ui.Info(this, $"Перенос завершён.\n\nСтрок в файле: {rec.Total}\nНовых клиентов: {rec.Created}\nОбъединено с существующими: {rec.Merged}\n" +
                      $"Изменено через Excel: {rec.Updated}\nБез изменений: {rec.Unchanged}\nПустых строк: {rec.Empty}\n\n" +
                      (rec.NoLoss ? "Сверка: потерь нет." : "Сверка: есть расхождение, проверьте файл.") +
                      "\nРезервная копия данных до переноса сохранена.");
    }

    void MergeSelected()
    {
        if (mDups.CurrentRow?.Tag is not int g || g >= dupGroups.Count) { Ui.Error(this, "Выберите строку группы дублей."); return; }
        var group = dupGroups[g];
        if (!Ui.Confirm(this, $"Объединить {group.Count} записи в «{group[0].Name}»?\nЗаявки и история перенесутся в основную запись, пустые поля заполнятся из дублей.")) return;
        Db.Backup("Перед объединением дублей");
        Duplicates.Merge(group);
        Db.Save();
        RefreshAll();
    }

    void MergeAll()
    {
        if (dupGroups.Count == 0) return;
        if (!Ui.Confirm(this, $"Объединить все группы дублей ({dupGroups.Count})? Перед этим будет создана резервная копия.")) return;
        Db.Backup("Перед объединением всех дублей");
        foreach (var g in dupGroups) Duplicates.Merge(g);
        Db.Save();
        RefreshAll();
    }

    void RefreshImport()
    {
        mLoading = true;
        try
        {
            mMap.Rows.Clear();
            if (plan != null)
            {
                mFile.Text = $"Файл «{plan.FileName}»: {plan.Rows.Count} строк";
                for (int i = 0; i < plan.Headers.Length; i++)
                {
                    var sample = plan.Rows.Select(r => i < r.Length ? r[i] : "").FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
                    var row = mMap.Rows.Add(plan.Headers[i], sample, Importer.FieldTitle(plan.Map[i]));
                    mMap.Rows[row].Cells[0].ReadOnly = mMap.Rows[row].Cells[1].ReadOnly = true;
                }
            }
            else mFile.Text = "Файл не выбран";
            UpdatePreview();

            mHistory.Rows.Clear();
            foreach (var h in Db.Data.Imports)
            {
                var i = mHistory.Rows.Add(h.Date.ToString("dd.MM.yyyy HH:mm"), h.FileName, h.Total, h.Created, h.Merged, h.Updated, h.Empty, h.NoLoss ? "Потерь нет" : "Расхождение");
                mHistory.Rows[i].Cells[7].Style.ForeColor = h.NoLoss ? Ui.Ok : Ui.Bad;
            }

            dupGroups = Duplicates.Groups(Db.Data.Clients);
            mDups.Rows.Clear();
            for (int g = 0; g < dupGroups.Count; g++)
                for (int k = 0; k < dupGroups[g].Count; k++)
                {
                    var c = dupGroups[g][k];
                    var i = mDups.Rows.Add(g + 1, k == 0 ? "Основная" : "Дубль", c.Name,
                        string.Join(" · ", new[] { c.Phone, c.Email, c.Inn }.Where(v => v != "")), Db.UserName(c.OwnerId));
                    mDups.Rows[i].Tag = g;
                    mDups.Rows[i].Cells[1].Style.ForeColor = k == 0 ? Ui.Ok : Ui.Warn;
                }
        }
        finally { mLoading = false; }
    }
}
