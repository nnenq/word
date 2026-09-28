namespace CrmSales;

/// <summary>Новая заявка клиента.</summary>
public class DealEditForm : Form
{
    readonly string clientId;
    readonly TextBox title = new() { Width = 320, PlaceholderText = "Например, поставка оборудования" };
    readonly NumericUpDown amount = new() { Maximum = 1_000_000_000, Increment = 1000, Value = 100000, ThousandsSeparator = true, Width = 160 };
    readonly ComboBox status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };

    public DealEditForm(string clientId)
    {
        this.clientId = clientId;
        Ui.Style(this);
        Text = "Новая заявка";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(380, 290);

        status.Items.AddRange(new[] { DealStatus.New, DealStatus.InWork, DealStatus.Offer }.Select(Names.Of).ToArray());
        status.SelectedIndex = 0;

        var save = Ui.Btn("Создать", (s, e) => Save(), primary: true);
        var cancel = Ui.Btn("Отмена", null);
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = save;
        CancelButton = cancel;

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
        panel.Controls.AddRange(new Control[]
        {
            Ui.Text("Название заявки *"), title, Ui.Text("Сумма, ₽"), amount, Ui.Text("Статус"), status, Ui.Row(save, cancel),
        });
        Controls.Add(panel);
    }

    void Save()
    {
        if (title.Text.Trim() == "") { Ui.Error(this, "Укажите название заявки."); title.Focus(); return; }
        var c = Db.ClientById(clientId)!;
        var d = new Deal
        {
            ClientId = clientId, OwnerId = c.OwnerId, Title = title.Text.Trim(), Amount = amount.Value,
            Status = Names.Parse<DealStatus>(status.Text, Names.Of),
        };
        Db.Data.Deals.Add(d);
        Db.AddHistory(clientId, InteractionType.System, $"Создана заявка «{d.Title}» на {Ui.Money(d.Amount)}");
        c.Updated = DateTime.Now;
        Db.Log($"Создана заявка «{d.Title}» ({c.Name})");
        Db.Save();
        DialogResult = DialogResult.OK;
    }
}
