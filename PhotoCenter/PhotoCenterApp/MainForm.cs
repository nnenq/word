// Главное окно: вкладки «Клиенты», «Сотрудники», «Услуги», «Заказы», «История заказов».
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PhotoCenterApp
{
    class MainForm : Form
    {
        private static readonly string[] Statuses = { "Принят", "В работе", "Выполнен", "Выдан", "Отменён" };

        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };

        // Клиенты
        private readonly DataGridView _clients = CreateGrid();
        private readonly TextBox _clientSearch = new TextBox { Width = 250 };
        // Сотрудники
        private readonly DataGridView _employees = CreateGrid();
        // Услуги
        private readonly DataGridView _services = CreateGrid();
        // Заказы
        private readonly DataGridView _orders = CreateGrid();
        private readonly DataGridView _orderItems = CreateGrid();
        private readonly ComboBox _statusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        private readonly ComboBox _newStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        // История
        private readonly RadioButton _byClient = new RadioButton { Text = "Клиент", Checked = true, AutoSize = true };
        private readonly RadioButton _byEmployee = new RadioButton { Text = "Сотрудник", AutoSize = true };
        private readonly ComboBox _person = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private readonly DataGridView _history = CreateGrid();
        private readonly Label _historyTotal = new Label { AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };

        public MainForm()
        {
            Text = "Фотоцентр — учёт клиентов, сотрудников, услуг и заказов   [сервер: " + Db.Server + ", база: " + Db.DatabaseName + "]";
            Font = new Font("Segoe UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 720);
            MinimumSize = new Size(900, 550);

            _tabs.TabPages.Add(BuildClientsTab());
            _tabs.TabPages.Add(BuildEmployeesTab());
            _tabs.TabPages.Add(BuildServicesTab());
            _tabs.TabPages.Add(BuildOrdersTab());
            _tabs.TabPages.Add(BuildHistoryTab());
            Controls.Add(_tabs);

            var menu = new MenuStrip();
            var dbMenu = new ToolStripMenuItem("База данных");
            dbMenu.DropDownItems.Add("Обновить все данные", null, delegate { ReloadAll(); });
            dbMenu.DropDownItems.Add("Пересоздать базу с тестовыми данными...", null, RecreateDatabase);
            dbMenu.DropDownItems.Add(new ToolStripSeparator());
            dbMenu.DropDownItems.Add("Выход", null, delegate { Close(); });
            menu.Items.Add(dbMenu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            Load += delegate { ReloadAll(); };
            _tabs.SelectedIndexChanged += delegate { if (_tabs.SelectedIndex == 4) LoadHistory(); };
        }

        // ================== Общие помощники ==================

        private static DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
            };
            g.DataBindingComplete += delegate
            {
                foreach (DataGridViewColumn col in g.Columns)
                {
                    if (col.ValueType == typeof(decimal)) col.DefaultCellStyle.Format = "N2";
                    if (col.ValueType == typeof(DateTime))
                        col.DefaultCellStyle.Format = col.HeaderText.StartsWith("Дата заказа") || col.HeaderText == "Выполнен" || col.HeaderText == "Дата"
                            ? "dd.MM.yyyy HH:mm" : "dd.MM.yyyy";
                }
            };
            return g;
        }

        private static FlowLayoutPanel CreateToolbar()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(6, 6, 6, 0), WrapContents = false };
        }

        private static Button Btn(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(3, 0, 6, 0) };
            b.Click += click;
            return b;
        }

        private static Label Lbl(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(8, 6, 3, 0) };
        }

        /// <summary>Код выбранной строки (первая колонка таблицы) или null.</summary>
        private static int? SelectedId(DataGridView grid)
        {
            if (grid.CurrentRow == null) return null;
            return Convert.ToInt32(grid.CurrentRow.Cells[0].Value);
        }

        private static void Error(Exception ex)
        {
            MessageBox.Show(Db.ErrorText(ex), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void NeedSelection(string what)
        {
            MessageBox.Show("Сначала выберите " + what + " в таблице.", "Подсказка", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ReloadAll()
        {
            try
            {
                LoadClients();
                LoadEmployees();
                LoadServices();
                LoadOrders();
                LoadPeople();
            }
            catch (Exception ex) { Error(ex); }
        }

        private void RecreateDatabase(object sender, EventArgs e)
        {
            if (MessageBox.Show("Все изменения будут удалены, база создастся заново с тестовыми данными. Продолжить?",
                    "Пересоздание базы", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (ConnectForm.CreateDatabase(this)) ReloadAll();
        }

        // ================== Клиенты ==================

        private TabPage BuildClientsTab()
        {
            var page = new TabPage("Клиенты");
            var bar = CreateToolbar();
            bar.Controls.Add(Btn("Добавить клиента", delegate { EditClient(null); }));
            bar.Controls.Add(Btn("Изменить", delegate { EditClient(SelectedId(_clients)); }));
            bar.Controls.Add(Btn("История заказов", delegate { ShowHistory(true, SelectedId(_clients)); }));
            bar.Controls.Add(Lbl("Поиск (ФИО или телефон):"));
            bar.Controls.Add(_clientSearch);
            _clientSearch.TextChanged += delegate { try { LoadClients(); } catch (Exception ex) { Error(ex); } };
            _clients.CellDoubleClick += delegate { EditClient(SelectedId(_clients)); };
            page.Controls.Add(_clients);
            page.Controls.Add(bar);
            return page;
        }

        private void LoadClients()
        {
            _clients.DataSource = Db.Query(
                @"SELECT c.ClientID AS [Код], c.LastName AS [Фамилия], c.FirstName AS [Имя], c.MiddleName AS [Отчество],
                         c.Phone AS [Телефон], c.Email AS [Email], c.RegDate AS [Дата регистрации],
                         (SELECT COUNT(*) FROM Orders o WHERE o.ClientID = c.ClientID) AS [Заказов]
                  FROM Clients c
                  WHERE @s IS NULL OR c.LastName + ' ' + c.FirstName + ' ' + ISNULL(c.MiddleName, '') LIKE '%' + @s + '%'
                        OR c.Phone LIKE '%' + @s + '%'
                  ORDER BY c.LastName, c.FirstName",
                Db.P("@s", _clientSearch.Text));
        }

        private void EditClient(int? id)
        {
            DataRow row = null;
            try
            {
                if (id != null)
                    row = Db.Query("SELECT * FROM Clients WHERE ClientID = @id", Db.P("@id", id)).Rows[0];

                var f = new FieldForm(id == null ? "Новый клиент" : "Клиент № " + id);
                f.AddText("last", "Фамилия", row == null ? null : row["LastName"], true);
                f.AddText("first", "Имя", row == null ? null : row["FirstName"], true);
                f.AddText("middle", "Отчество", row == null ? null : row["MiddleName"], false);
                f.AddText("phone", "Телефон", row == null ? null : row["Phone"], true);
                f.AddText("email", "Email", row == null ? null : row["Email"], false);
                if (f.ShowDialog(this) != DialogResult.OK) return;

                if (id == null)
                    Db.Proc("sp_AddClient", Db.P("@LastName", f["last"]), Db.P("@FirstName", f["first"]), Db.P("@MiddleName", f["middle"]),
                        Db.P("@Phone", f["phone"]), Db.P("@Email", f["email"]));
                else
                    Db.Proc("sp_UpdateClient", Db.P("@ClientID", id), Db.P("@LastName", f["last"]), Db.P("@FirstName", f["first"]),
                        Db.P("@MiddleName", f["middle"]), Db.P("@Phone", f["phone"]), Db.P("@Email", f["email"]));
                LoadClients();
                LoadPeople();
            }
            catch (Exception ex) { Error(ex); }
        }

        // ================== Сотрудники ==================

        private TabPage BuildEmployeesTab()
        {
            var page = new TabPage("Сотрудники");
            var bar = CreateToolbar();
            bar.Controls.Add(Btn("Добавить сотрудника", delegate { EditEmployee(null); }));
            bar.Controls.Add(Btn("Изменить", delegate { EditEmployee(SelectedId(_employees)); }));
            bar.Controls.Add(Btn("История заказов", delegate { ShowHistory(false, SelectedId(_employees)); }));
            _employees.CellDoubleClick += delegate { EditEmployee(SelectedId(_employees)); };
            page.Controls.Add(_employees);
            page.Controls.Add(bar);
            return page;
        }

        private void LoadEmployees()
        {
            _employees.DataSource = Db.Query(
                @"SELECT e.EmployeeID AS [Код], e.LastName AS [Фамилия], e.FirstName AS [Имя], e.MiddleName AS [Отчество],
                         e.Position AS [Должность], e.Phone AS [Телефон], e.HireDate AS [Дата приёма], e.Salary AS [Оклад],
                         (SELECT COUNT(*) FROM Orders o WHERE o.EmployeeID = e.EmployeeID) AS [Заказов]
                  FROM Employees e ORDER BY e.LastName, e.FirstName");
        }

        private void EditEmployee(int? id)
        {
            try
            {
                DataRow row = id == null ? null : Db.Query("SELECT * FROM Employees WHERE EmployeeID = @id", Db.P("@id", id)).Rows[0];
                var f = new FieldForm(id == null ? "Новый сотрудник" : "Сотрудник № " + id);
                f.AddText("last", "Фамилия", row == null ? null : row["LastName"], true);
                f.AddText("first", "Имя", row == null ? null : row["FirstName"], true);
                f.AddText("middle", "Отчество", row == null ? null : row["MiddleName"], false);
                f.AddText("position", "Должность", row == null ? "Фотограф" : row["Position"], true);
                f.AddText("phone", "Телефон", row == null ? null : row["Phone"], true);
                f.AddNumber("salary", "Оклад", row == null ? 6000m : row["Salary"], 0, 1000000, 2);
                if (f.ShowDialog(this) != DialogResult.OK) return;

                if (id == null)
                    Db.Proc("sp_AddEmployee", Db.P("@LastName", f["last"]), Db.P("@FirstName", f["first"]), Db.P("@MiddleName", f["middle"]),
                        Db.P("@Position", f["position"]), Db.P("@Phone", f["phone"]), Db.P("@Salary", f["salary"]));
                else
                    Db.Proc("sp_UpdateEmployee", Db.P("@EmployeeID", id), Db.P("@LastName", f["last"]), Db.P("@FirstName", f["first"]),
                        Db.P("@MiddleName", f["middle"]), Db.P("@Position", f["position"]), Db.P("@Phone", f["phone"]), Db.P("@Salary", f["salary"]));
                LoadEmployees();
                LoadPeople();
            }
            catch (Exception ex) { Error(ex); }
        }

        // ================== Услуги ==================

        private TabPage BuildServicesTab()
        {
            var page = new TabPage("Услуги");
            var bar = CreateToolbar();
            bar.Controls.Add(Btn("Добавить услугу", delegate { EditService(null); }));
            bar.Controls.Add(Btn("Изменить", delegate { EditService(SelectedId(_services)); }));
            _services.CellDoubleClick += delegate { EditService(SelectedId(_services)); };
            page.Controls.Add(_services);
            page.Controls.Add(bar);
            return page;
        }

        private void LoadServices()
        {
            _services.DataSource = Db.Query(
                @"SELECT ServiceID AS [Код], Name AS [Услуга], Description AS [Описание], Price AS [Цена], Unit AS [Ед.]
                  FROM Services ORDER BY Name");
        }

        private void EditService(int? id)
        {
            try
            {
                DataRow row = id == null ? null : Db.Query("SELECT * FROM Services WHERE ServiceID = @id", Db.P("@id", id)).Rows[0];
                var f = new FieldForm(id == null ? "Новая услуга" : "Услуга № " + id);
                f.AddText("name", "Название", row == null ? null : row["Name"], true);
                f.AddText("descr", "Описание", row == null ? null : row["Description"], false);
                f.AddNumber("price", "Цена", row == null ? 100m : row["Price"], 0.01m, 1000000, 2);
                f.AddText("unit", "Единица", row == null ? "шт." : row["Unit"], true);
                if (f.ShowDialog(this) != DialogResult.OK) return;

                if (id == null)
                    Db.Proc("sp_AddService", Db.P("@Name", f["name"]), Db.P("@Description", f["descr"]), Db.P("@Price", f["price"]), Db.P("@Unit", f["unit"]));
                else
                    Db.Proc("sp_UpdateService", Db.P("@ServiceID", id), Db.P("@Name", f["name"]), Db.P("@Description", f["descr"]),
                        Db.P("@Price", f["price"]), Db.P("@Unit", f["unit"]));
                LoadServices();
            }
            catch (Exception ex) { Error(ex); }
        }

        // ================== Заказы ==================

        private TabPage BuildOrdersTab()
        {
            var page = new TabPage("Заказы");
            var bar = CreateToolbar();
            bar.Controls.Add(Btn("Новый заказ", NewOrder));
            bar.Controls.Add(Btn("Изменить заказ", EditOrder));
            bar.Controls.Add(Btn("Добавить услугу в заказ", AddOrderItem));
            bar.Controls.Add(Lbl("Статус:"));
            _newStatus.Items.AddRange(Statuses);
            _newStatus.SelectedIndex = 1;
            bar.Controls.Add(_newStatus);
            bar.Controls.Add(Btn("Установить", SetStatus));
            bar.Controls.Add(Lbl("Показать:"));
            _statusFilter.Items.Add("Все");
            _statusFilter.Items.AddRange(Statuses);
            _statusFilter.SelectedIndex = 0;
            _statusFilter.SelectedIndexChanged += delegate { try { LoadOrders(); } catch (Exception ex) { Error(ex); } };
            bar.Controls.Add(_statusFilter);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            split.Panel1.Controls.Add(_orders);
            var itemsTitle = new Label { Text = "Состав выбранного заказа:", Dock = DockStyle.Top, Height = 26, Padding = new Padding(4, 5, 0, 0) };
            split.Panel2.Controls.Add(_orderItems);
            split.Panel2.Controls.Add(itemsTitle);
            page.Controls.Add(split);
            page.Controls.Add(bar);
            Shown += delegate { split.SplitterDistance = (int)(split.Height * 0.62); };

            _orders.SelectionChanged += delegate { LoadOrderItems(); };
            _orders.CellDoubleClick += EditOrder;
            return page;
        }

        private void LoadOrders()
        {
            int? keep = SelectedId(_orders);
            string status = _statusFilter.SelectedIndex <= 0 ? null : _statusFilter.SelectedItem.ToString();
            _orders.DataSource = Db.Query("SELECT * FROM vOrders WHERE @st IS NULL OR [Статус] = @st ORDER BY [Дата заказа] DESC",
                Db.P("@st", status));
            if (keep != null)
                foreach (DataGridViewRow r in _orders.Rows)
                    if (Convert.ToInt32(r.Cells[0].Value) == keep) { _orders.CurrentCell = r.Cells[0]; break; }
            LoadOrderItems();
        }

        private void LoadOrderItems()
        {
            try
            {
                int? id = SelectedId(_orders);
                _orderItems.DataSource = Db.Query("SELECT * FROM vOrderDetails WHERE [Номер заказа] = @id", Db.P("@id", id ?? -1));
            }
            catch (Exception ex) { Error(ex); }
        }

        private DataTable ClientsList()
        {
            return Db.Query("SELECT ClientID, LastName + ' ' + FirstName + ' (' + Phone + ')' AS Name FROM Clients ORDER BY LastName, FirstName");
        }

        private DataTable EmployeesList()
        {
            return Db.Query("SELECT EmployeeID, LastName + ' ' + FirstName + N' — ' + Position AS Name FROM Employees ORDER BY LastName, FirstName");
        }

        private DataTable ServicesList()
        {
            return Db.Query("SELECT ServiceID, Name + N' — ' + CAST(Price AS NVARCHAR(20)) + N' руб./' + Unit AS Name FROM Services ORDER BY Name");
        }

        private void NewOrder(object sender, EventArgs e)
        {
            try
            {
                var f = new FieldForm("Новый заказ");
                f.AddCombo("client", "Клиент", ClientsList(), "ClientID", "Name", null);
                f.AddCombo("employee", "Сотрудник", EmployeesList(), "EmployeeID", "Name", null);
                f.AddDate("due", "Срок выполнения", DateTime.Today.AddDays(3));
                f.AddCombo("service", "Услуга", ServicesList(), "ServiceID", "Name", null);
                f.AddNumber("qty", "Количество", 1m, 1, 10000, 0);
                f.AddText("comment", "Комментарий", null, false);
                if (f.ShowDialog(this) != DialogResult.OK) return;

                DataTable res = Db.Proc("sp_AddOrder", Db.P("@ClientID", f["client"]), Db.P("@EmployeeID", f["employee"]),
                    Db.P("@DueDate", f["due"]), Db.P("@Comment", f["comment"]));
                int orderId = Convert.ToInt32(res.Rows[0][0]);
                Db.Proc("sp_AddOrderItem", Db.P("@OrderID", orderId), Db.P("@ServiceID", f["service"]), Db.P("@Quantity", Convert.ToInt32(f["qty"])));
                _statusFilter.SelectedIndex = 0;
                LoadOrders();
                SelectOrder(orderId);
                LoadClients();
                LoadEmployees();
                MessageBox.Show("Заказ № " + orderId + " создан. Другие услуги можно добавить кнопкой «Добавить услугу в заказ».",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Error(ex); }
        }

        private void SelectOrder(int orderId)
        {
            foreach (DataGridViewRow r in _orders.Rows)
                if (Convert.ToInt32(r.Cells[0].Value) == orderId) { _orders.CurrentCell = r.Cells[0]; break; }
        }

        private void EditOrder(object sender, EventArgs e)
        {
            int? id = SelectedId(_orders);
            if (id == null) { NeedSelection("заказ"); return; }
            try
            {
                DataRow row = Db.Query("SELECT * FROM Orders WHERE OrderID = @id", Db.P("@id", id)).Rows[0];
                var f = new FieldForm("Заказ № " + id);
                f.AddCombo("employee", "Сотрудник", EmployeesList(), "EmployeeID", "Name", row["EmployeeID"]);
                f.AddDate("due", "Срок выполнения", row["DueDate"]);
                f.AddText("comment", "Комментарий", row["Comment"], false);
                if (f.ShowDialog(this) != DialogResult.OK) return;
                Db.Proc("sp_UpdateOrder", Db.P("@OrderID", id), Db.P("@EmployeeID", f["employee"]), Db.P("@DueDate", f["due"]), Db.P("@Comment", f["comment"]));
                LoadOrders();
                LoadEmployees();
            }
            catch (Exception ex) { Error(ex); }
        }

        private void AddOrderItem(object sender, EventArgs e)
        {
            int? id = SelectedId(_orders);
            if (id == null) { NeedSelection("заказ"); return; }
            try
            {
                var f = new FieldForm("Добавить услугу в заказ № " + id);
                f.AddCombo("service", "Услуга", ServicesList(), "ServiceID", "Name", null);
                f.AddNumber("qty", "Количество", 1m, 1, 10000, 0);
                if (f.ShowDialog(this) != DialogResult.OK) return;
                Db.Proc("sp_AddOrderItem", Db.P("@OrderID", id), Db.P("@ServiceID", f["service"]), Db.P("@Quantity", Convert.ToInt32(f["qty"])));
                LoadOrders();
            }
            catch (Exception ex) { Error(ex); }
        }

        private void SetStatus(object sender, EventArgs e)
        {
            int? id = SelectedId(_orders);
            if (id == null) { NeedSelection("заказ"); return; }
            try
            {
                Db.Proc("sp_SetOrderStatus", Db.P("@OrderID", id), Db.P("@Status", _newStatus.SelectedItem.ToString()));
                LoadOrders();
            }
            catch (Exception ex) { Error(ex); }
        }

        // ================== История заказов ==================

        private TabPage BuildHistoryTab()
        {
            var page = new TabPage("История заказов");
            var bar = CreateToolbar();
            bar.Controls.Add(Lbl("Показать историю:"));
            bar.Controls.Add(_byClient);
            bar.Controls.Add(_byEmployee);
            bar.Controls.Add(_person);
            bar.Controls.Add(Btn("Показать", delegate { LoadHistory(); }));
            bar.Controls.Add(_historyTotal);
            _byClient.Margin = _byEmployee.Margin = new Padding(6, 6, 3, 0);
            _historyTotal.Margin = new Padding(12, 6, 3, 0);
            _byClient.CheckedChanged += delegate { LoadPeople(); };
            _person.SelectedIndexChanged += delegate { LoadHistory(); };
            page.Controls.Add(_history);
            page.Controls.Add(bar);
            return page;
        }

        private bool _loadingPeople;

        private void LoadPeople()
        {
            _loadingPeople = true;
            try
            {
                _person.DataSource = null;
                _person.DisplayMember = "Name";
                _person.ValueMember = _byClient.Checked ? "ClientID" : "EmployeeID";
                _person.DataSource = _byClient.Checked ? ClientsList() : EmployeesList();
            }
            catch (Exception ex) { Error(ex); }
            finally { _loadingPeople = false; }
            LoadHistory();
        }

        private void LoadHistory()
        {
            if (_loadingPeople || _person.SelectedValue == null || _person.SelectedValue is DataRowView) return;
            try
            {
                DataTable t = _byClient.Checked
                    ? Db.Proc("sp_ClientOrderHistory", Db.P("@ClientID", _person.SelectedValue))
                    : Db.Proc("sp_EmployeeOrderHistory", Db.P("@EmployeeID", _person.SelectedValue));
                _history.DataSource = t;

                string sumColumn = _byClient.Checked ? "Стоимость" : "Сумма";
                decimal total = 0;
                var orders = new System.Collections.Generic.HashSet<int>();
                foreach (DataRow r in t.Rows)
                {
                    orders.Add(Convert.ToInt32(r["Номер заказа"]));
                    if (r[sumColumn] != DBNull.Value && r["Статус"].ToString() != "Отменён") total += Convert.ToDecimal(r[sumColumn]);
                }
                _historyTotal.Text = "Заказов: " + orders.Count + ", сумма (без отменённых): " + total.ToString("N2") + " руб.";
            }
            catch (Exception ex) { Error(ex); }
        }

        /// <summary>Переход на вкладку истории для выбранного клиента или сотрудника.</summary>
        private void ShowHistory(bool client, int? id)
        {
            if (id == null) { NeedSelection(client ? "клиента" : "сотрудника"); return; }
            _tabs.SelectedIndex = 4;
            if (client) _byClient.Checked = true; else _byEmployee.Checked = true;
            _person.SelectedValue = id.Value;
            LoadHistory();
        }
    }
}
