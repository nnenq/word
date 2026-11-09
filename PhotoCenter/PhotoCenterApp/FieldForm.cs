// Универсальное окно ввода данных: поля добавляются кодом (текст, число, дата, выпадающий список).
// Используется для добавления и редактирования клиентов, сотрудников, услуг и заказов.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PhotoCenterApp
{
    class FieldForm : Form
    {
        private class Field
        {
            public string Label;
            public Control Input;
            public bool Required;
        }

        private readonly Dictionary<string, Field> _fields = new Dictionary<string, Field>();
        private readonly List<string> _order = new List<string>();
        private int _y = 15;

        public FieldForm(string title)
        {
            Text = title;
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(470, 100);
        }

        private void Add(string key, string label, Control input, bool required)
        {
            var lbl = new Label { Text = label + (required ? " *" : ""), Location = new Point(15, _y + 4), AutoSize = true };
            input.Location = new Point(170, _y);
            input.Width = 280;
            Controls.Add(lbl);
            Controls.Add(input);
            _fields[key] = new Field { Label = label, Input = input, Required = required };
            _order.Add(key);
            _y += 38;
        }

        public void AddText(string key, string label, object value, bool required)
        {
            Add(key, label, new TextBox { Text = value == null || value == DBNull.Value ? "" : value.ToString() }, required);
        }

        public void AddNumber(string key, string label, object value, decimal min, decimal max, int decimals)
        {
            var num = new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, ThousandsSeparator = true };
            if (decimals > 0) num.Increment = 10;
            decimal v = value == null || value == DBNull.Value ? min : Convert.ToDecimal(value);
            num.Value = Math.Max(min, Math.Min(max, v));
            Add(key, label, num, true);
        }

        public void AddDate(string key, string label, object value)
        {
            var date = new DateTimePicker { Format = DateTimePickerFormat.Short };
            date.Value = value == null || value == DBNull.Value ? DateTime.Today : Convert.ToDateTime(value);
            Add(key, label, date, true);
        }

        public void AddDateTime(string key, string label, object value)
        {
            var date = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm" };
            date.Value = value == null || value == DBNull.Value ? DateTime.Now : Convert.ToDateTime(value);
            Add(key, label, date, true);
        }

        /// <summary>Выпадающий список из таблицы (например, список клиентов): показывается textColumn, значение — valueColumn.</summary>
        public void AddCombo(string key, string label, DataTable table, string valueColumn, string textColumn, object selected)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.DisplayMember = textColumn;
            combo.ValueMember = valueColumn;
            combo.DataSource = table;
            Add(key, label, combo, true);
            if (selected != null && selected != DBNull.Value)
                Shown += delegate { combo.SelectedValue = selected; };
        }

        public void AddList(string key, string label, string[] items, string selected)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items);
            combo.SelectedItem = selected ?? items[0];
            Add(key, label, combo, true);
        }

        /// <summary>Значение поля после нажатия «Сохранить».</summary>
        public object this[string key]
        {
            get
            {
                Control c = _fields[key].Input;
                if (c is NumericUpDown) return ((NumericUpDown)c).Value;
                var picker = c as DateTimePicker;
                if (picker != null)
                {
                    DateTime v = picker.Value;
                    return picker.Format == DateTimePickerFormat.Custom
                        ? new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0)
                        : v.Date;
                }
                var combo = c as ComboBox;
                if (combo != null) return combo.DataSource != null ? combo.SelectedValue : combo.SelectedItem;
                return c.Text.Trim();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            var ok = new Button { Text = "Сохранить", Location = new Point(230, _y + 10), Size = new Size(105, 34) };
            var cancel = new Button { Text = "Отмена", Location = new Point(345, _y + 10), Size = new Size(105, 34), DialogResult = DialogResult.Cancel };
            ok.Click += OkClick;
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(470, _y + 60);
            base.OnLoad(e);
        }

        private void OkClick(object sender, EventArgs e)
        {
            foreach (string key in _order)
            {
                Field f = _fields[key];
                bool empty = (f.Input is TextBox && f.Input.Text.Trim().Length == 0) ||
                             (f.Input is ComboBox && ((ComboBox)f.Input).SelectedIndex < 0);
                if (f.Required && empty)
                {
                    MessageBox.Show("Заполните поле «" + f.Label + "».", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    f.Input.Focus();
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }
    }
}
