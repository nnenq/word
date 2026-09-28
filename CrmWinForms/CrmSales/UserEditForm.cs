namespace CrmSales;

/// <summary>Добавление пользователя или смена пароля (выполняет администратор).</summary>
public class UserEditForm : Form
{
    readonly User? user;
    readonly TextBox name = new() { Width = 300 };
    readonly TextBox login = new() { Width = 300 };
    readonly ComboBox role = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly TextBox password = new() { Width = 300, UseSystemPasswordChar = true };
    readonly TextBox password2 = new() { Width = 300, UseSystemPasswordChar = true };

    public UserEditForm(User? user)
    {
        this.user = user;
        Ui.Style(this);
        Text = user == null ? "Новый пользователь" : "Смена пароля: " + user.Name;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(360, user == null ? 420 : 230);

        role.Items.AddRange(Names.All<Role>(Names.Of));
        role.SelectedItem = Names.Of(Role.Manager);

        var save = Ui.Btn("Сохранить", (s, e) => Save(), primary: true);
        var cancel = Ui.Btn("Отмена", null);
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = save;
        CancelButton = cancel;

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
        if (user == null) panel.Controls.AddRange(new Control[] { Ui.Text("ФИО *"), name, Ui.Text("Логин *"), login, Ui.Text("Роль"), role });
        panel.Controls.AddRange(new Control[] { Ui.Text("Пароль * (не короче 4 символов)"), password, Ui.Text("Повторите пароль"), password2, Ui.Row(save, cancel) });
        Controls.Add(panel);
    }

    void Save()
    {
        if (password.Text.Length < 4) { Ui.Error(this, "Пароль должен быть не короче 4 символов."); return; }
        if (password.Text != password2.Text) { Ui.Error(this, "Пароли не совпадают."); return; }

        if (user == null)
        {
            var n = name.Text.Trim();
            var l = login.Text.Trim().ToLowerInvariant();
            if (n == "" || l == "") { Ui.Error(this, "Заполните ФИО и логин."); return; }
            if (Db.Data.Users.Any(u => u.Login.Equals(l, StringComparison.OrdinalIgnoreCase))) { Ui.Error(this, $"Логин «{l}» уже занят."); return; }
            Db.Data.Users.Add(new User { Name = n, Login = l, PasswordHash = Db.Hash(l, password.Text), Role = Names.Parse<Role>(role.Text, Names.Of) });
            Db.Log($"Добавлен пользователь {n} ({l}), роль: {role.Text}");
        }
        else
        {
            user.PasswordHash = Db.Hash(user.Login, password.Text);
            Db.Log($"Изменён пароль пользователя {user.Name}");
        }
        Db.Save();
        DialogResult = DialogResult.OK;
    }
}
