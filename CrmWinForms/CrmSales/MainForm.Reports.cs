namespace CrmSales;

// Вкладка «Отчёты» для руководителя
public partial class MainForm
{
    FlowLayoutPanel rKpis = null!;
    BarChart rFunnel = null!;
    BarChart rMonths = null!;
    DataGridView rManagers = null!;
    DataGridView rCriteria = null!;
    Label rDate = null!;

    Control BuildReports()
    {
        rDate = Ui.Text("", Ui.Small, Ui.Muted);
        rKpis = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };

        rFunnel = new BarChart();
        rMonths = new BarChart { Vertical = true };
        var charts = new TableLayoutPanel { Dock = DockStyle.Top, Height = 260, ColumnCount = 2, Margin = new Padding(0, 0, 0, 12) };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charts.Controls.Add(Ui.Box("Воронка заявок", rFunnel), 0, 0);
        charts.Controls.Add(Ui.Box("Успешные продажи по месяцам, тыс. ₽", rMonths), 1, 0);

        rManagers = Ui.Grid();
        rManagers.Columns.AddRange(Ui.Col("Менеджер", 160), Ui.Col("Клиентов", 70, true), Ui.Col("Открыто заявок", 80, true),
            Ui.Col("Продажи", 110, true), Ui.Col("Конверсия", 80, true), Ui.Col("Ср. обработка", 90, true));

        rCriteria = CriteriaGrid();

        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var head = Ui.Row(Ui.Text("Отчёты для руководителя", Ui.H1), rDate, Ui.Btn("Выгрузить базу и заявки в Excel", (s, e) => ExportExcel()));
        var managersBox = Ui.Box("Результаты менеджеров", rManagers, 300);
        var criteriaBox = Ui.Box("Критерии успешности внедрения", rCriteria, 280);
        content.Controls.AddRange(new Control[] { head, rKpis, charts, managersBox, criteriaBox });
        content.Resize += (s, e) =>
        {
            var w = content.ClientSize.Width - 24;
            foreach (Control c in content.Controls) c.Width = w;
        };
        return content;
    }

    static DataGridView CriteriaGrid()
    {
        var g = Ui.Grid();
        g.Columns.AddRange(Ui.Col("Статус", 60), Ui.Col("Критерий", 200), Ui.Col("Текущее значение", 220));
        return g;
    }

    static void FillCriteria(DataGridView g)
    {
        g.Rows.Clear();
        foreach (var c in Metrics.Criteria())
        {
            var i = g.Rows.Add(Metrics.StateText(c.State), c.Title, c.Detail);
            g.Rows[i].Cells[0].Style.ForeColor = Ui.StateColor(c.State);
            g.Rows[i].Cells[0].Style.Font = Ui.Bold;
        }
    }

    void RefreshReports()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var deals = Db.Data.Deals;
        var won = deals.Where(d => d.Status == DealStatus.Won).ToList();
        var lost = deals.Count(d => d.Status == DealStatus.Lost);
        var open = deals.Where(d => !d.IsClosed).ToList();
        var conv = won.Count + lost == 0 ? 0 : won.Count * 100.0 / (won.Count + lost);
        var avgH = Metrics.AvgDealHours(deals);

        rDate.Text = $"данные на {now:dd.MM.yyyy HH:mm}";
        rKpis.Controls.Clear();
        rKpis.Controls.Add(Ui.Kpi("Продажи за месяц", Ui.Money(won.Where(d => d.Closed >= monthStart).Sum(d => d.Amount))));
        rKpis.Controls.Add(Ui.Kpi("В работе", Ui.Money(open.Sum(d => d.Amount)), $"{open.Count} заявок"));
        rKpis.Controls.Add(Ui.Kpi("Конверсия", $"{conv:0} %", "успешные / закрытые"));
        rKpis.Controls.Add(Ui.Kpi("Обработка заявки", $"{avgH:0.0} ч", $"до внедрения {Db.Data.Settings.BaselineDealHours} ч"));

        rFunnel.SetItems(Enum.GetValues<DealStatus>().Select(s =>
        {
            var ds = deals.Where(d => d.Status == s).ToList();
            return (Names.Of(s), (double)ds.Count, $"{ds.Count} · {Ui.Money(ds.Sum(d => d.Amount))}");
        }));

        rMonths.SetItems(Enumerable.Range(0, 6).Reverse().Select(i =>
        {
            var from = monthStart.AddMonths(-i);
            var sum = won.Where(d => d.Closed >= from && d.Closed < from.AddMonths(1)).Sum(d => d.Amount);
            return (from.ToString("MMM"), (double)sum, $"{sum / 1000:N0}");
        }));

        rManagers.Rows.Clear();
        foreach (var u in Db.Managers.OrderByDescending(u => deals.Where(d => d.OwnerId == u.Id && d.Status == DealStatus.Won).Sum(d => d.Amount)))
        {
            var ds = deals.Where(d => d.OwnerId == u.Id).ToList();
            var closed = ds.Where(d => d.Closed != null).ToList();
            var w = ds.Where(d => d.Status == DealStatus.Won).ToList();
            rManagers.Rows.Add(u.Name + (u.Pilot ? "  (пилот)" : ""), Db.Data.Clients.Count(c => c.OwnerId == u.Id), ds.Count(d => !d.IsClosed),
                Ui.Money(w.Sum(d => d.Amount)), closed.Count == 0 ? "—" : $"{w.Count * 100.0 / closed.Count:0} %",
                closed.Count == 0 ? "—" : $"{closed.Average(d => d.Hours):0.0} ч");
        }
        FillCriteria(rCriteria);
    }
}
