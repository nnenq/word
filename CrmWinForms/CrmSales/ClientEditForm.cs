using System.Text.RegularExpressions;

namespace CrmSales;

/// <summary>Создание и редактирование клиента с проверкой на дубли.</summary>
public class ClientEditForm : Form
{
    readonly Client? existing;
    readonly TextBox name = new(), contact = new(), phone = new(), email = new(), inn = new(), city = new();
    readonly ComboBox source = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox owner = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Label dupWarn;

    public Client? Saved { get; private set; }

    public ClientEditForm(Client? client)
    {
        existing = client;
        Ui.Style(this);
        Text = client == null ? "Новый клиент" : "Редактирование клиента";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(560, 470);

        source.Items.AddRange(new object[] { "Сайт", "Звонок", "Выставка", "Рекомендация", "Тендер", "Почта", "Excel" });
        foreach (var m in Db.Managers.Where(u => u.Active)) owner.Items.Add(new Item(m.Id, m.Name));
        owner.Enabled = Db.SeeAll; // менеджер создаёт клиентов только на себя

        var c = client ?? new Client { OwnerId = Db.Current!.Role == Role.Manager ? Db.Current.Id : Db.Managers.First().Id, Source = "Звонок" };
        name.Text = c.Name; contact.Text = c.Contact; phone.Text = c.Phone; email.Text = c.Email; inn.Text = c.Inn; city.Text = c.City;
        source.SelectedItem = source.Items.Contains(c.Source) ? c.Source : "Звонок";
        owner.SelectedItem = owner.Items.Cast<Item>().FirstOrDefault(i => i.Id == c.OwnerId) ?? (owner.Items.Count > 0 ? owner.Items[0] : null);

        dupWarn = Ui.Note("", Ui.Warn, Ui.WarnSoft);
        dupWarn.MaximumSize = new Size(500, 0);
        dupWarn.Visible = false;
        foreach (var tb in new[] { name, phone, email, inn }) tb.Leave += (s, e) => CheckDuplicate();

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(string label, Control ctl)
        {
            ctl.Dock = DockStyle.Fill;
            grid.Controls.Add(Ui.Text(label));
            grid.Controls.Add(ctl);
        }
        Add("Компания или ФИО *", name);
        Add("Контактное лицо", contact);
        Add("Телефон", phone);
        Add("Email", email);
        Add("ИНН", inn);
        Add("Город", city);
        Add("Источник", source);
        Add("Ответственный менеджер", owner);
        grid.Controls.Add(dupWarn);
        grid.SetColumnSpan(dupWarn, 2);

        var save = Ui.Btn("Сохранить", (s, e) => Save(), primary: true);
        var cancel = Ui.Btn("Отмена", null);
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = save;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(12, 8, 12, 8) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(grid);
        Controls.Add(buttons);
    }

    Client Probe() => new()
    {
        Id = existing?.Id ?? "new", Name = name.Text.Trim(), Phone = phone.Text.Trim(), Email = email.Text.Trim(), Inn = inn.Text.Trim(),
    };

    Client? CheckDuplicate()
    {
        var d = Duplicates.Find(Probe(), Db.Data.Clients);
        dupWarn.Visible = d != null;
        if (d != null)
            dupWarn.Text = $"Похоже на существующего клиента: «{d.Name}» ({new[] { d.Phone, d.Email, d.Inn }.FirstOrDefault(v => v != "")}), менеджер {Db.UserName(d.OwnerId)}. Проверьте, чтобы не создать дубль.";
        return d;
    }

    void Save()
    {
        if (name.Text.Trim() == "") { Ui.Error(this, "Укажите название компании или ФИО клиента."); name.Focus(); return; }
        if (email.Text.Trim() != "" && !Regex.IsMatch(email.Text.Trim(), @"^\S+@\S+\.\S+$")) { Ui.Error(this, "Email указан с ошибкой. Пример: info@company.ru"); email.Focus(); return; }
        if (inn.Text.Trim() != "" && Duplicates.NormInn(inn.Text) is { Length: not (10 or 12) }) { Ui.Error(this, "ИНН должен содержать 10 цифр (организация) или 12 цифр (ИП)."); inn.Focus(); return; }

        var dup = CheckDuplicate();
        if (dup != null && !Ui.Confirm(this, $"В базе уже есть похожий клиент «{dup.Name}».\nВсё равно сохранить?")) return;

        var c = existing ?? new Client();
        var oldOwner = c.OwnerId;
        c.Name = name.Text.Trim();
        c.Contact = contact.Text.Trim();
        c.Phone = phone.Text.Trim();
        c.Email = email.Text.Trim();
        c.Inn = inn.Text.Trim();
        c.City = city.Text.Trim();
        c.Source = source.SelectedItem as string ?? "";
        c.OwnerId = (owner.SelectedItem as Item)?.Id ?? Db.Current!.Id;
        c.Updated = DateTime.Now;

        if (existing == null)
        {
            Db.Data.Clients.Add(c);
            Db.AddHistory(c.Id, InteractionType.System, "Клиент добавлен в CRM");
            Db.Log($"Создан клиент «{c.Name}»");
        }
        else
        {
            if (oldOwner != c.OwnerId)
            {
                foreach (var d in Db.Data.Deals.Where(d => d.ClientId == c.Id && !d.IsClosed)) d.OwnerId = c.OwnerId;
                Db.AddHistory(c.Id, InteractionType.System, $"Ответственный изменён: {Db.UserName(oldOwner)} → {Db.UserName(c.OwnerId)}");
            }
            Db.Log($"Изменён клиент «{c.Name}»");
        }
        Db.Save();
        Saved = c;
        DialogResult = DialogResult.OK;
    }
}
