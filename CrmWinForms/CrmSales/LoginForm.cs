namespace CrmSales;

public class LoginForm : Form
{
    readonly TextBox login = new() { Width = 340 };
    readonly TextBox password = new() { Width = 340, UseSystemPasswordChar = true };
    readonly Label error;

    public LoginForm()
    {
        Ui.Style(this);
        Text = "CRM · Отдел продаж — вход";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 470);

        error = Ui.Note("", Ui.Bad, Ui.BadSoft);
        error.Visible = false;
        error.MaximumSize = new Size(340, 0);

        var enter = Ui.Btn("Войти", (s, e) => TryLogin(), primary: true);
        AcceptButton = enter;

        var demo = new ListBox { Width = 340, Height = 110, BorderStyle = BorderStyle.FixedSingle, Font = Ui.Small };
        (string Login, string Pass, string Role)[] accounts =
        {
            ("admin", "admin", "Администратор"), ("head", "head", "Руководитель отдела"),
            ("petrov", "1234", "Менеджер (пилотная группа)"), ("fedorova", "1234", "Менеджер"),
        };
        foreach (var a in accounts) demo.Items.Add($"{a.Login} / {a.Pass} — {a.Role}");
        demo.Click += (s, e) =>
        {
            if (demo.SelectedIndex < 0) return;
            login.Text = accounts[demo.SelectedIndex].Login;
            password.Text = accounts[demo.SelectedIndex].Pass;
        };
        demo.DoubleClick += (s, e) => TryLogin();

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(30, 24, 30, 20), BackColor = Color.White,
        };
        panel.Controls.AddRange(new Control[]
        {
            Ui.Text("CRM · Отдел продаж", Ui.H1),
            Ui.Text("Единая база клиентов, заявок и истории работы", Ui.Small, Ui.Muted),
            Ui.Text("Логин"), login,
            Ui.Text("Пароль"), password,
            error, enter,
            Ui.Text("Демо-учётные записи (щёлкните, чтобы подставить):", Ui.Small, Ui.Muted), demo,
        });
        enter.Margin = new Padding(0, 10, 0, 14);
        Controls.Add(panel);
        Shown += (s, e) => login.Focus();
    }

    void TryLogin()
    {
        var name = login.Text.Trim();
        var user = Db.Data.Users.FirstOrDefault(u => string.Equals(u.Login, name, StringComparison.OrdinalIgnoreCase)
                                                     && u.PasswordHash == Db.Hash(name, password.Text));
        if (user == null) { ShowError("Неверный логин или пароль. Проверьте раскладку клавиатуры и Caps Lock."); return; }
        if (!user.Active) { ShowError("Учётная запись заблокирована. Обратитесь к системному администратору."); return; }

        Db.Current = user;
        Db.Log("Вход в систему");
        Db.Save();
        DialogResult = DialogResult.OK;
    }

    void ShowError(string text)
    {
        error.Text = text;
        error.Visible = true;
        password.SelectAll();
        password.Focus();
    }
}
