using System.Diagnostics;

namespace CrmSales;

// Вкладки «Клиенты» и «Заявки»
public partial class MainForm
{
    TextBox cSearch = null!;
    ComboBox cOwner = null!;
    DataGridView cGrid = null!;
    LinkLabel cDups = null!;
    Label cCount = null!;
    Stopwatch? searchWatch; // замер времени поиска клиента (критерий успешности)

    ComboBox dFilter = null!;
    DataGridView dGrid = null!;
    Label dSum = null!;

    Control BuildClients()
    {
        cSearch = new TextBox { Width = 380, PlaceholderText = "Поиск: компания, контакт, телефон, email, ИНН" };
        cSearch.TextChanged += (s, e) =>
        {
            if (cSearch.Text.Length == 0) searchWatch = null;
            else searchWatch ??= Stopwatch.StartNew();
            RefreshClients();
        };
        cSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Down) cGrid.Focus(); };

        cOwner = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Visible = Db.SeeAll };
        cOwner.Items.Add(new Item("all", "Все менеджеры"));
        foreach (var m in Db.Managers) cOwner.Items.Add(new Item(m.Id, m.Name));
        cOwner.SelectedIndex = 0;
        cOwner.SelectedIndexChanged += (s, e) => RefreshClients();

        var top = Ui.Row(cSearch, cOwner,
            Ui.Btn("Новый клиент", (s, e) => NewClient(), primary: true),
            Ui.Btn("Открыть карточку", (s, e) => OpenClient(Ui.SelectedTag(cGrid))),
            Ui.Btn("Выгрузить в Excel", (s, e) => ExportExcel()));

        cDups = new LinkLabel { AutoSize = true, LinkColor = Ui.Warn, ForeColor = Ui.Warn, Margin = new Padding(0, 0, 0, 6), Visible = false };
        cDups.LinkClicked += (s, e) => GoTo("Перенос данных");

        cGrid = Ui.Grid();
        cGrid.Columns.AddRange(Ui.Col("Компания", 170), Ui.Col("Контакт", 120), Ui.Col("Телефон", 110), Ui.Col("Email", 140),
            Ui.Col("Город", 90), Ui.Col("Менеджер", 110), Ui.Col("Открытые заявки", 70, true));
        cGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenClient(Ui.SelectedTag(cGrid)); };
        cGrid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; OpenClient(Ui.SelectedTag(cGrid)); } };

        cCount = Ui.Text("", Ui.Small, Ui.Muted);
        return Ui.Stack(2, top, cDups, cGrid, cCount);
    }

    void RefreshClients()
    {
        IEnumerable<Client> list = Db.VisibleClients();
        if (Db.SeeAll && cOwner.SelectedItem is Item { Id: not "all" } owner) list = list.Where(c => c.OwnerId == owner.Id);

        var q = cSearch.Text.Trim().ToLowerInvariant();
        if (q != "")
        {
            var qPhone = new string(q.Where(char.IsDigit).ToArray());
            if (qPhone.Length == 11 && qPhone[0] is '7' or '8') qPhone = qPhone[1..];
            list = list.Where(c =>
                new[] { c.Name, c.Contact, c.Email, c.City, c.Inn }.Any(v => v.ToLowerInvariant().Contains(q)) ||
                (qPhone.Length >= 3 && Duplicates.NormPhone(c.Phone).Contains(qPhone)));
        }

        var rows = list.OrderByDescending(c => c.Updated).ToList();
        cGrid.Rows.Clear();
        foreach (var c in rows)
        {
            var open = Db.Data.Deals.Count(d => d.ClientId == c.Id && !d.IsClosed);
            var i = cGrid.Rows.Add(c.Name, c.Contact, c.Phone, c.Email, c.City, Db.UserName(c.OwnerId), open == 0 ? "—" : open.ToString());
            cGrid.Rows[i].Tag = c.Id;
        }
        cCount.Text = q == "" ? $"Клиентов: {rows.Count}" : $"Найдено: {rows.Count}";

        var dups = Db.SeeAll ? Duplicates.Groups(Db.Data.Clients).Count : 0;
        cDups.Visible = dups > 0 && Db.CanManage;
        cDups.Text = $"Найдено возможных дублей: {dups}. Проверить и объединить →";
    }

    void NewClient()
    {
        using var f = new ClientEditForm(null);
        if (f.ShowDialog(this) == DialogResult.OK && f.Saved != null) OpenClient(f.Saved.Id);
        else RefreshAll();
    }

    // ---------- Заявки ----------

    Control BuildDeals()
    {
        dFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        dFilter.Items.AddRange(new object[] { "Открытые", "Успешные", "Отказы", "Все" });
        dFilter.SelectedIndex = 0;
        dFilter.SelectedIndexChanged += (s, e) => RefreshDeals();
        dSum = Ui.Text("", Ui.Bold);

        dGrid = Ui.Grid();
        dGrid.Columns.AddRange(Ui.Col("Клиент", 150), Ui.Col("Заявка", 150), Ui.Col("Сумма", 90, true), Ui.Col("Статус", 90),
            Ui.Col("Менеджер", 110), Ui.Col("Создана", 80), Ui.Col("Срок обработки", 110, true));
        dGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenClient(Ui.SelectedTag(dGrid)); };

        var hint = Ui.Text("Красным выделены открытые заявки, которые обрабатываются дольше нормы. Двойной щелчок открывает карточку клиента.", Ui.Small, Ui.Muted);
        return Ui.Stack(1, Ui.Row(Ui.Text("Показать:"), dFilter, Ui.Btn("Выгрузить в Excel", (s, e) => ExportExcel()), dSum), dGrid, hint);
    }

    void RefreshDeals()
    {
        IEnumerable<Deal> list = Db.VisibleDeals();
        list = dFilter.SelectedIndex switch
        {
            0 => list.Where(d => !d.IsClosed),
            1 => list.Where(d => d.Status == DealStatus.Won),
            2 => list.Where(d => d.Status == DealStatus.Lost),
            _ => list,
        };
        var rows = list.OrderByDescending(d => d.Created).ToList();
        var norm = Db.Data.Settings.BaselineDealHours;
        dGrid.Rows.Clear();
        foreach (var d in rows)
        {
            var late = !d.IsClosed && d.Hours > norm;
            var time = d.IsClosed ? $"{d.Hours:0} ч" : d.Hours < 48 ? $"{d.Hours:0} ч" : $"{d.Hours / 24:0} дн." + (late ? " — просрочена" : "");
            var i = dGrid.Rows.Add(Db.ClientById(d.ClientId)?.Name ?? "—", d.Title, Ui.Money(d.Amount), Names.Of(d.Status),
                Db.UserName(d.OwnerId), d.Created.ToString("dd.MM.yyyy"), time);
            dGrid.Rows[i].Tag = d.ClientId;
            if (late) dGrid.Rows[i].Cells[6].Style.ForeColor = Ui.Bad;
            dGrid.Rows[i].Cells[3].Style.ForeColor = d.Status switch
            {
                DealStatus.Won => Ui.Ok, DealStatus.Lost => Ui.Bad, DealStatus.InWork => Ui.Warn, _ => Ui.Ink,
            };
        }
        dSum.Text = $"   Заявок: {rows.Count} · на сумму {Ui.Money(rows.Sum(d => d.Amount))}";
    }
}
