namespace CrmSales;

// Вкладка «Внедрение»: план, критерии, риски, обратная связь, аварийный откат
public partial class MainForm
{
    DataGridView iPlan = null!, iCrit = null!, iRisks = null!, iFeedback = null!;
    Label iPlanInfo = null!, iPilot = null!, iFbSummary = null!, iVerdict = null!;
    NumericUpDown iBaseSearch = null!, iBaseDeal = null!, iCantWork = null!;
    CheckBox iDataLoss = null!;
    bool iLoading;

    Control BuildImpl()
    {
        var inner = new TabControl { Dock = DockStyle.Fill };
        inner.TabPages.Add(Page("План внедрения", BuildPlan()));
        inner.TabPages.Add(Page("Критерии успеха", BuildCrit()));
        inner.TabPages.Add(Page("Риски", BuildRisks()));
        inner.TabPages.Add(Page("Обратная связь", BuildFeedback()));
        inner.TabPages.Add(Page("Аварийный откат", BuildRollback()));
        inner.SelectedIndexChanged += (s, e) => RefreshImpl();
        return inner;
    }

    static TabPage Page(string title, Control content)
    {
        var p = new TabPage(title) { Padding = new Padding(10), BackColor = Ui.Back };
        content.Dock = DockStyle.Fill;
        p.Controls.Add(content);
        return p;
    }

    Control BuildPlan()
    {
        iPlanInfo = Ui.Text("", Ui.H2);
        iPlan = Ui.Grid();
        iPlan.ReadOnly = !Db.CanManage;
        iPlan.Columns.AddRange(Ui.Col("№", 25, true), Ui.Col("Этап", 160), Ui.Col("Содержание", 220));
        iPlan.Columns.Add(Ui.Combo("Статус", Names.All<StageStatus>(Names.Of), 70));
        iPlan.Columns[3].ReadOnly = !Db.CanManage;
        iPlan.CellValueChanged += (s, e) =>
        {
            if (iLoading || e.RowIndex < 0 || e.ColumnIndex != 3) return;
            var st = Db.Data.Plan.First(x => x.Id == (string)iPlan.Rows[e.RowIndex].Tag!);
            st.Status = Names.Parse<StageStatus>(iPlan.Rows[e.RowIndex].Cells[3].Value as string, Names.Of);
            Db.Log($"Этап «{st.Title}»: {Names.Of(st.Status)}");
            Db.Save();
            BeginInvoke(RefreshImpl);
        };
        iPilot = Ui.Note("", Ui.Ink, Color.White);
        var strategy = Ui.Text("Стратегия: пилотная + поэтапная. Сначала система работает у пилотной группы 2–4 недели, затем по итогам обратной связи подключается весь отдел. Состав группы меняет администратор.", Ui.Small, Ui.Muted);
        strategy.MaximumSize = new Size(1100, 0);
        return Ui.Stack(1, iPlanInfo, iPlan, iPilot, strategy);
    }

    Control BuildCrit()
    {
        iCrit = CriteriaGrid();
        iBaseSearch = new NumericUpDown { Minimum = 1, Maximum = 3600, Width = 90 };
        iBaseDeal = new NumericUpDown { Minimum = 1, Maximum = 1000, Width = 90 };
        var save = Ui.Btn("Сохранить", (s, e) =>
        {
            Db.Data.Settings.BaselineSearchSec = (int)iBaseSearch.Value;
            Db.Data.Settings.BaselineDealHours = (int)iBaseDeal.Value;
            Db.Log("Изменены показатели до внедрения");
            Db.Save();
            RefreshAll();
        }, primary: true);
        var baseline = Ui.Row(Ui.Text("Показатели до внедрения (Excel и почта): поиск клиента, с"), iBaseSearch,
            Ui.Text("обработка заявки, ч"), iBaseDeal, save);
        baseline.Enabled = Db.CanManage;
        var hint = Ui.Text("Время поиска измеряется автоматически: от первого символа в строке поиска до открытия карточки клиента.", Ui.Small, Ui.Muted);
        return Ui.Stack(0, iCrit, baseline, hint);
    }

    Control BuildRisks()
    {
        iRisks = Ui.Grid();
        iRisks.ReadOnly = !Db.CanManage;
        iRisks.Columns.AddRange(Ui.Col("Риск", 180), Ui.Col("Вероятность", 70), Ui.Col("Меры по снижению", 260));
        iRisks.Columns.Add(Ui.Combo("Статус", Names.All<RiskStatus>(Names.Of), 90));
        iRisks.Columns[3].ReadOnly = !Db.CanManage;
        iRisks.CellValueChanged += (s, e) =>
        {
            if (iLoading || e.RowIndex < 0 || e.ColumnIndex != 3) return;
            var r = Db.Data.Risks.First(x => x.Id == (string)iRisks.Rows[e.RowIndex].Tag!);
            r.Status = Names.Parse<RiskStatus>(iRisks.Rows[e.RowIndex].Cells[3].Value as string, Names.Of);
            Db.Log($"Риск «{r.Title}»: {Names.Of(r.Status)}");
            Db.Save();
        };
        return Ui.Stack(0, iRisks);
    }

    Control BuildFeedback()
    {
        var kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        kind.Items.AddRange(Names.All<FeedbackKind>(Names.Of));
        kind.SelectedIndex = 0;
        var rating = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 5, Width = 60 };
        var text = new TextBox { Multiline = true, Height = 60, Width = 700, PlaceholderText = "Что получилось или что мешает работать" };
        var send = Ui.Btn("Отправить отзыв", (s, e) =>
        {
            if (text.Text.Trim() == "") { Ui.Error(this, "Напишите комментарий к отзыву."); return; }
            Db.Data.Feedback.Add(new Feedback
            {
                UserId = Db.Current!.Id, Kind = Names.Parse<FeedbackKind>(kind.Text, Names.Of), Rating = (int)rating.Value, Text = text.Text.Trim(),
            });
            Db.Log("Оставлен отзыв о системе");
            Db.Save();
            text.Clear();
            RefreshImpl();
            Ui.Info(this, "Спасибо, отзыв сохранён.");
        }, primary: true);

        iFbSummary = Ui.Text("", Ui.Bold);
        iFeedback = Ui.Grid();
        iFeedback.Columns.AddRange(Ui.Col("Дата", 70), Ui.Col("Сотрудник", 110), Ui.Col("Тип", 80), Ui.Col("Оценка", 50, true),
            Ui.Col("Отзыв", 330), Ui.Col("Статус", 70));
        var resolve = Ui.Btn("Отметить решённым", (s, e) =>
        {
            var f = Db.Data.Feedback.FirstOrDefault(x => x.Id == Ui.SelectedTag(iFeedback));
            if (f == null) return;
            f.Resolved = true;
            Db.Log("Отзыв отмечен решённым");
            Db.Save();
            RefreshImpl();
        });
        resolve.Visible = Db.CanManage;

        return Ui.Stack(3,
            Ui.Row(Ui.Text("Тип:"), kind, Ui.Text("Оценка удобства (1–5):"), rating),
            Ui.Row(text, send),
            Ui.Row(iFbSummary, resolve),
            iFeedback);
    }

    Control BuildRollback()
    {
        iDataLoss = new CheckBox { Text = "Произошла потеря или повреждение данных", AutoSize = true };
        iCantWork = new NumericUpDown { Minimum = 0, Maximum = 100, Width = 60 };
        iVerdict = Ui.Note("", Ui.Ink, Color.White);
        iDataLoss.CheckedChanged += (s, e) => UpdateVerdict();
        iCantWork.ValueChanged += (s, e) => UpdateVerdict();

        var steps = Ui.Text(
            "Порядок действий при откате:\n" +
            "1. Руководитель проекта принимает решение об откате.\n" +
            "2. Пользователи получают уведомление о временном возврате к прежнему способу работы.\n" +
            "3. Клиентская база выгружается в Excel, работа временно возвращается в Excel и почту.\n" +
            "4. Данные восстанавливаются из резервной копии.\n" +
            "5. Технический специалист анализирует причину сбоя и устраняет её.\n" +
            "6. После устранения причины система запускается повторно.");
        steps.MaximumSize = new Size(1100, 0);

        var buttons = Ui.Row(Ui.Btn("Выгрузить базу в Excel (возврат к старой системе)", (s, e) => ExportExcel()));
        if (Db.IsAdmin) buttons.Controls.Add(Ui.Btn("Восстановить из резервной копии…", (s, e) => GoTo("Администрирование")));
        else buttons.Controls.Add(Ui.Text("Восстановление из копии выполняет администратор.", Ui.Small, Ui.Muted));

        return Ui.Stack(5,
            Ui.Text("Откат выполняется, если в первые 3 дня пилотной эксплуатации выявлена критическая ошибка. Отметьте, что произошло:"),
            iDataLoss,
            Ui.Row(Ui.Text("Не могут работать в системе:"), iCantWork, Ui.Text($"из {Db.Data.Users.Count(u => u.Pilot)} менеджеров пилотной группы")),
            iVerdict, steps, buttons);
    }

    void UpdateVerdict()
    {
        int pilot = Math.Max(1, Db.Data.Users.Count(u => u.Pilot));
        iCantWork.Maximum = pilot;
        double share = (double)iCantWork.Value * 100 / pilot;
        bool need = iDataLoss.Checked || share > 30;
        iVerdict.Text = need
            ? "Требуется аварийный откат. " + (iDataLoss.Checked ? "Зафиксирована потеря данных. " : "") +
              (share > 30 ? $"Не могут работать {share:0} % пилотной группы (порог 30 %)." : "")
            : $"Критических условий нет, откат не требуется. Не могут работать {share:0} % пилотной группы при пороге 30 %.";
        iVerdict.ForeColor = need ? Ui.Bad : Ui.Ok;
        iVerdict.BackColor = need ? Ui.BadSoft : Ui.OkSoft;
    }

    void RefreshImpl()
    {
        iLoading = true;
        try
        {
            iPlan.Rows.Clear();
            int n = 1;
            foreach (var s in Db.Data.Plan)
            {
                var i = iPlan.Rows.Add(n++, s.Title, s.Hint, Names.Of(s.Status));
                iPlan.Rows[i].Tag = s.Id;
                iPlan.Rows[i].Cells[3].Style.ForeColor = s.Status switch { StageStatus.Done => Ui.Ok, StageStatus.InProgress => Ui.Warn, _ => Ui.Muted };
            }
            iPlanInfo.Text = $"9 этапов внедрения · выполнено {Db.Data.Plan.Count(s => s.Status == StageStatus.Done)} из {Db.Data.Plan.Count}";
            iPilot.Text = "Пилотная группа: " + string.Join(", ", Db.Data.Users.Where(u => u.Pilot).Select(u => u.Name));

            FillCriteria(iCrit);
            iBaseSearch.Value = Math.Clamp(Db.Data.Settings.BaselineSearchSec, 1, 3600);
            iBaseDeal.Value = Math.Clamp(Db.Data.Settings.BaselineDealHours, 1, 1000);

            iRisks.Rows.Clear();
            foreach (var r in Db.Data.Risks)
            {
                var i = iRisks.Rows.Add(r.Title, r.Probability, r.Measure, Names.Of(r.Status));
                iRisks.Rows[i].Tag = r.Id;
                iRisks.Rows[i].Cells[1].Style.ForeColor = r.Probability == "Высокая" ? Ui.Bad : r.Probability == "Средняя" ? Ui.Warn : Ui.Ok;
            }

            iFeedback.Rows.Clear();
            foreach (var f in Db.Data.Feedback.OrderByDescending(f => f.Date))
            {
                var i = iFeedback.Rows.Add(f.Date.ToString("dd.MM.yyyy"), Db.UserName(f.UserId), Names.Of(f.Kind), $"{f.Rating}/5", f.Text,
                    f.Resolved ? "Решено" : f.Kind == FeedbackKind.Problem ? "Открыто" : "");
                iFeedback.Rows[i].Tag = f.Id;
                iFeedback.Rows[i].Cells[2].Style.ForeColor = f.Kind switch { FeedbackKind.Problem => Ui.Bad, FeedbackKind.Idea => Ui.Accent, _ => Ui.Ok };
            }
            var fb = Db.Data.Feedback;
            iFbSummary.Text = $"Отзывов: {fb.Count} · средняя оценка: {(fb.Count == 0 ? 0 : fb.Average(f => f.Rating)):0.0} · нерешённых проблем: {fb.Count(f => f.Kind == FeedbackKind.Problem && !f.Resolved)}";
            UpdateVerdict();
        }
        finally { iLoading = false; }
    }
}
