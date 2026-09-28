namespace CrmSales;

/// <summary>Карточка клиента: контакты, заявки и единая история взаимодействия.</summary>
public class ClientCardForm : Form
{
    readonly string clientId;
    readonly Label title = Ui.Text("", Ui.H1);
    readonly Label meta = Ui.Text("", Ui.Small, Ui.Muted);
    readonly Label contacts = Ui.Text("");
    readonly Label totals = Ui.Text("", Ui.Bold);
    readonly DataGridView deals = Ui.Grid();
    readonly DataGridView history = Ui.Grid();
    readonly ComboBox status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    readonly ComboBox itype = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    readonly TextBox itext = new() { Multiline = true, Height = 60, Dock = DockStyle.Fill, PlaceholderText = "Что обсудили, о чём договорились, следующий шаг" };
    readonly bool canEdit;

    Client C => Db.ClientById(clientId)!;

    public ClientCardForm(string id)
    {
        clientId = id;
        Ui.Style(this);
        Size = new Size(1150, 720);
        MinimumSize = new Size(900, 560);
        canEdit = Db.CanEdit(C);

        status.Items.AddRange(Names.All<DealStatus>(Names.Of));
        itype.Items.AddRange(new[] { InteractionType.Call, InteractionType.Mail, InteractionType.Meeting, InteractionType.Note }.Select(Names.Of).ToArray());
        itype.SelectedIndex = 0;

        var edit = Ui.Btn("Редактировать", (s, e) =>
        {
            using var f = new ClientEditForm(C);
            if (f.ShowDialog(this) == DialogResult.OK) Reload();
        });
        var delete = Ui.Btn("Удалить клиента", (s, e) => Delete());
        delete.ForeColor = Ui.Bad;
        edit.Visible = canEdit;
        delete.Visible = Db.IsAdmin;
        var close = Ui.Btn("Закрыть", (s, e) => Close());
        CancelButton = close;

        deals.Columns.AddRange(Ui.Col("Заявка", 150), Ui.Col("Сумма", 90, true), Ui.Col("Статус", 90), Ui.Col("Создана", 75), Ui.Col("Закрыта", 75));
        var newDeal = Ui.Btn("Новая заявка", (s, e) =>
        {
            using var f = new DealEditForm(clientId);
            if (f.ShowDialog(this) == DialogResult.OK) Reload();
        }, primary: true);
        var apply = Ui.Btn("Изменить статус", (s, e) =>
        {
            var d = Db.Data.Deals.FirstOrDefault(x => x.Id == Ui.SelectedTag(deals));
            if (d == null) { Ui.Error(this, "Выберите заявку в таблице."); return; }
            Db.SetDealStatus(d, Names.Parse<DealStatus>(status.Text, Names.Of));
            Reload();
        });
        deals.SelectionChanged += (s, e) =>
        {
            var d = Db.Data.Deals.FirstOrDefault(x => x.Id == Ui.SelectedTag(deals));
            if (d != null) status.SelectedItem = Names.Of(d.Status);
        };
        var dealButtons = Ui.Row(newDeal, status, apply);
        dealButtons.Visible = canEdit;

        history.Columns.AddRange(Ui.Col("Дата", 90), Ui.Col("Тип", 60), Ui.Col("Сотрудник", 90), Ui.Col("Содержание", 260));
        var add = Ui.Btn("Добавить запись", (s, e) => AddHistory(), primary: true);
        var input = new TableLayoutPanel { Height = 100, ColumnCount = 1, Dock = DockStyle.Top };
        input.Controls.Add(Ui.Row(Ui.Text("Тип:"), itype, add));
        input.Controls.Add(itext);
        input.Visible = canEdit;

        var left = Ui.Stack(3, Ui.Box("Контакты", contacts, 170), totals, dealButtons, deals);
        var right = Ui.Stack(1, input, history);
        var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        cols.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cols.Controls.Add(Ui.Box("Контакты и заявки", left), 0, 0);
        cols.Controls.Add(Ui.Box("История взаимодействия", right), 1, 0);

        var root = Ui.Stack(2, Ui.Row(title, edit, delete, close), meta, cols);
        root.Padding = new Padding(14);
        Controls.Add(root);
        Reload();
    }

    void Reload()
    {
        var c = C;
        Text = "Клиент: " + c.Name;
        title.Text = c.Name;
        meta.Text = $"Менеджер: {Db.UserName(c.OwnerId)} · в базе с {c.Created:dd.MM.yyyy} · источник: {(c.Source == "" ? "—" : c.Source)}";
        string V(string v) => v == "" ? "—" : v;
        contacts.Text = $"Контактное лицо:  {V(c.Contact)}\nТелефон:  {V(c.Phone)}\nEmail:  {V(c.Email)}\nИНН:  {V(c.Inn)}\nГород:  {V(c.City)}";

        var ds = Db.Data.Deals.Where(d => d.ClientId == c.Id).OrderByDescending(d => d.Created).ToList();
        deals.Rows.Clear();
        foreach (var d in ds)
        {
            var i = deals.Rows.Add(d.Title, Ui.Money(d.Amount), Names.Of(d.Status), d.Created.ToString("dd.MM.yyyy"), d.Closed?.ToString("dd.MM.yyyy") ?? "");
            deals.Rows[i].Tag = d.Id;
            deals.Rows[i].Cells[2].Style.ForeColor = d.Status switch { DealStatus.Won => Ui.Ok, DealStatus.Lost => Ui.Bad, DealStatus.InWork => Ui.Warn, _ => Ui.Ink };
        }
        totals.Text = $"Заявок: {ds.Count} · открытых: {ds.Count(d => !d.IsClosed)} · продажи: {Ui.Money(ds.Where(d => d.Status == DealStatus.Won).Sum(d => d.Amount))}";

        history.Rows.Clear();
        foreach (var h in Db.Data.Interactions.Where(i => i.ClientId == c.Id).OrderByDescending(i => i.Date))
        {
            var i = history.Rows.Add(h.Date.ToString("dd.MM.yyyy HH:mm"), Names.Of(h.Type), Db.UserName(h.UserId), h.Text);
            if (h.Type == InteractionType.System) history.Rows[i].DefaultCellStyle.ForeColor = Ui.Muted;
        }
    }

    void AddHistory()
    {
        var text = itext.Text.Trim();
        if (text == "") { Ui.Error(this, "Опишите, что обсудили с клиентом."); itext.Focus(); return; }
        Db.AddHistory(clientId, Names.Parse<InteractionType>(itype.Text, Names.Of), text);
        C.Updated = DateTime.Now;
        Db.Log($"Добавлена запись в историю «{C.Name}»");
        Db.Save();
        itext.Clear();
        Reload();
    }

    void Delete()
    {
        var c = C;
        if (!Ui.Confirm(this, $"Удалить клиента «{c.Name}» вместе с заявками и историей?\nПеред удалением будет создана резервная копия.")) return;
        Db.DeleteClient(c);
        Close();
    }
}
