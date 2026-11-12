// Оконная версия калькулятора. Использует те же методы класса Arithmetic, что и консольная.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Calculator;

namespace CalculatorForms
{
    class MainForm : Form
    {
        private readonly TextBox _a = new TextBox();
        private readonly TextBox _b = new TextBox();
        private readonly Label _result = new Label();
        private readonly ListBox _history = new ListBox();

        public MainForm()
        {
            Text = "Калькулятор — вызов методов";
            Font = new Font("Segoe UI", 11F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 430);

            Controls.Add(new Label { Text = "Число a:", Location = new Point(20, 22), AutoSize = true });
            _a.Location = new Point(110, 18);
            _a.Width = 310;
            Controls.Add(_a);

            Controls.Add(new Label { Text = "Число b:", Location = new Point(20, 62), AutoSize = true });
            _b.Location = new Point(110, 58);
            _b.Width = 310;
            Controls.Add(_b);

            AddButton("+", 0, Color.FromArgb(37, 99, 235), (a, b) => Arithmetic.Add(a, b));
            AddButton("−", 1, Color.FromArgb(234, 88, 12), (a, b) => Arithmetic.Subtract(a, b));
            AddButton("×", 2, Color.FromArgb(22, 163, 74), (a, b) => Arithmetic.Multiply(a, b));
            AddButton("÷", 3, Color.FromArgb(124, 58, 237), (a, b) => Arithmetic.Divide(a, b));

            _result.Location = new Point(20, 160);
            _result.Size = new Size(400, 40);
            _result.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            _result.Text = "Результат:";
            Controls.Add(_result);

            Controls.Add(new Label { Text = "История вычислений:", Location = new Point(20, 208), AutoSize = true });
            _history.Location = new Point(20, 235);
            _history.Size = new Size(400, 180);
            Controls.Add(_history);
        }

        private void AddButton(string sign, int index, Color color, Func<double, double, double> operation)
        {
            var button = new Button
            {
                Text = sign,
                Location = new Point(20 + index * 102, 100),
                Size = new Size(94, 46),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate { Calculate(sign, operation); };
            Controls.Add(button);
        }

        /// <summary>Читает числа, вызывает метод операции и показывает результат.</summary>
        private void Calculate(string sign, Func<double, double, double> operation)
        {
            double a, b;
            if (!TryRead(_a, out a) || !TryRead(_b, out b)) return;
            try
            {
                double result = operation(a, b);
                string line = string.Format("{0} {1} {2} = {3}", a, sign, b, Math.Round(result, 10));
                _result.ForeColor = Color.FromArgb(21, 128, 61);
                _result.Text = "Результат: " + Math.Round(result, 10);
                _history.Items.Insert(0, line);
            }
            catch (DivideByZeroException ex)
            {
                _result.ForeColor = Color.FromArgb(220, 38, 38);
                _result.Text = "Ошибка: " + ex.Message;
            }
        }

        private bool TryRead(TextBox box, out double value)
        {
            string text = box.Text.Trim().Replace(',', '.');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            _result.ForeColor = Color.FromArgb(220, 38, 38);
            _result.Text = "Введите число в поле «" + (box == _a ? "a" : "b") + "»";
            box.Focus();
            return false;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
