using System.Drawing.Drawing2D;

namespace CrmSales;

/// <summary>Общие цвета, шрифты и конструкторы элементов интерфейса.</summary>
public static class Ui
{
    public static readonly Color Accent = Color.FromArgb(15, 110, 92);
    public static readonly Color AccentSoft = Color.FromArgb(221, 239, 234);
    public static readonly Color Back = Color.FromArgb(243, 245, 244);
    public static readonly Color Ink = Color.FromArgb(24, 34, 31);
    public static readonly Color Muted = Color.FromArgb(94, 107, 103);
    public static readonly Color Line = Color.FromArgb(220, 226, 223);
    public static readonly Color Ok = Color.FromArgb(46, 125, 50);
    public static readonly Color OkSoft = Color.FromArgb(227, 242, 228);
    public static readonly Color Warn = Color.FromArgb(168, 98, 0);
    public static readonly Color WarnSoft = Color.FromArgb(251, 239, 217);
    public static readonly Color Bad = Color.FromArgb(179, 38, 30);
    public static readonly Color BadSoft = Color.FromArgb(249, 225, 223);

    public static readonly Font Font = new("Segoe UI", 10f);
    public static readonly Font Bold = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font H1 = new("Segoe UI Semibold", 16f);
    public static readonly Font H2 = new("Segoe UI Semibold", 12f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Big = new("Segoe UI Semibold", 18f);

    public static string Money(decimal v) => v.ToString("N0") + " ₽";
    public static string Money(double v) => Money((decimal)v);

    public static Color StateColor(CritState s) => s switch { CritState.Ok => Ok, CritState.Warn => Warn, _ => Bad };

    public static void Style(Form f)
    {
        f.Font = Font;
        f.BackColor = Back;
        f.ForeColor = Ink;
        f.StartPosition = FormStartPosition.CenterParent;
        f.ShowIcon = false;
    }

    public static Button Btn(string text, EventHandler? click, bool primary = false)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 4, 10, 4),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink,
            Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = primary ? Accent : Line;
        if (click != null) b.Click += click;
        return b;
    }

    public static Label Text(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text, AutoSize = true, Font = font ?? Font, ForeColor = color ?? Ink, Margin = new Padding(0, 4, 8, 4),
    };

    public static Label Note(string text, Color fore, Color back) => new()
    {
        Text = text, AutoSize = true, ForeColor = fore, BackColor = back, Padding = new Padding(8, 6, 8, 6),
        Margin = new Padding(0, 4, 0, 4), MaximumSize = new Size(1100, 0),
    };

    public static FlowLayoutPanel Row(params Control[] items)
    {
        var p = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8), Padding = new Padding(0),
        };
        p.Controls.AddRange(items);
        return p;
    }

    /// <summary>Вертикальный макет: строки с высотой по содержимому, кроме указанной, которая растягивается.</summary>
    public static TableLayoutPanel Stack(int fillRow, params Control[] rows)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows.Length, Padding = new Padding(0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < rows.Length; i++)
        {
            t.RowStyles.Add(i == fillRow ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            rows[i].Dock = DockStyle.Fill;
            t.Controls.Add(rows[i], 0, i);
        }
        return t;
    }

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle, GridColor = Line, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            EnableHeadersVisualStyles = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, EditMode = DataGridViewEditMode.EditOnEnter,
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = Back;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        g.ColumnHeadersDefaultCellStyle.Font = Small;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(4);
        g.DefaultCellStyle.SelectionBackColor = AccentSoft;
        g.DefaultCellStyle.SelectionForeColor = Ink;
        g.DefaultCellStyle.Padding = new Padding(4, 3, 4, 3);
        g.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        // Выпадающие списки в таблицах применяются сразу после выбора
        g.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (g.IsCurrentCellDirty && g.CurrentCell is DataGridViewComboBoxCell or DataGridViewCheckBoxCell)
                g.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        g.DataError += (s, e) => e.ThrowException = false;
        return g;
    }

    public static DataGridViewTextBoxColumn Col(string header, float weight = 100, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic };
        if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return c;
    }

    public static DataGridViewComboBoxColumn Combo(string header, string[] items, float weight = 100)
    {
        var c = new DataGridViewComboBoxColumn { HeaderText = header, FillWeight = weight, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
        c.Items.AddRange(items);
        return c;
    }

    public static string? SelectedTag(DataGridView g) => g.CurrentRow?.Tag as string;

    public static GroupBox Box(string title, Control content, int height = 0)
    {
        var b = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(10), Font = Bold, ForeColor = Ink, BackColor = Color.White };
        content.Dock = DockStyle.Fill;
        content.Font = Font;
        b.Controls.Add(content);
        if (height > 0) { b.Dock = DockStyle.Top; b.Height = height; }
        return b;
    }

    /// <summary>Плашка с крупным показателем для отчётов.</summary>
    public static Panel Kpi(string caption, string value, string hint = "")
    {
        var p = new Panel { Width = 250, Height = 92, BackColor = Color.White, Margin = new Padding(0, 0, 12, 12), Padding = new Padding(12) };
        p.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Line), 0, 0, p.Width - 1, p.Height - 1);
        p.Controls.Add(new Label { Text = hint, Font = Small, ForeColor = Muted, AutoSize = true, Location = new Point(12, 66) });
        p.Controls.Add(new Label { Text = value, Font = Big, ForeColor = Ink, AutoSize = true, Location = new Point(10, 28) });
        p.Controls.Add(new Label { Text = caption.ToUpperInvariant(), Font = Small, ForeColor = Muted, AutoSize = true, Location = new Point(12, 8) });
        return p;
    }

    public static bool Confirm(IWin32Window owner, string text, string title = "Подтверждение") =>
        MessageBox.Show(owner, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    public static void Info(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "CRM", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Error(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "CRM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

/// <summary>Элемент выпадающего списка с идентификатором.</summary>
public record Item(string Id, string Text)
{
    public override string ToString() => Text;
}

/// <summary>Простая столбчатая диаграмма без сторонних библиотек.</summary>
public class BarChart : Control
{
    public bool Vertical { get; set; }
    public List<(string Label, double Value, string Text)> Items { get; } = new();

    public BarChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = Ui.Small;
        MinimumSize = new Size(200, 180);
    }

    public void SetItems(IEnumerable<(string, double, string)> items)
    {
        Items.Clear();
        Items.AddRange(items);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Items.Count == 0) return;
        double max = Math.Max(1, Items.Max(i => i.Value));
        using var bar = new SolidBrush(Ui.Accent);
        using var track = new SolidBrush(Ui.Back);
        using var text = new SolidBrush(Ui.Ink);
        using var muted = new SolidBrush(Ui.Muted);
        using var line = new Pen(Ui.Line);

        if (!Vertical)
        {
            int labelW = 120, valueW = 150, pad = 8;
            int rowH = Math.Min(34, (Height - pad * 2) / Items.Count);
            int barW = Math.Max(10, Width - labelW - valueW - pad * 3);
            for (int i = 0; i < Items.Count; i++)
            {
                var (label, value, t) = Items[i];
                int y = pad + i * rowH;
                g.DrawString(label, Font, text, pad, y + rowH / 2f - 8);
                var r = new Rectangle(labelW + pad, y + rowH / 2 - 7, barW, 14);
                g.FillRectangle(track, r);
                g.FillRectangle(bar, r with { Width = (int)(barW * value / max) });
                g.DrawString(t, Font, text, labelW + barW + pad * 2, y + rowH / 2f - 8);
            }
        }
        else
        {
            int top = 22, bottom = 24, pad = 10;
            int h = Height - top - bottom;
            float slot = (Width - pad * 2) / (float)Items.Count;
            float w = Math.Min(48, slot * 0.6f);
            g.DrawLine(line, pad, top + h, Width - pad, top + h);
            for (int i = 0; i < Items.Count; i++)
            {
                var (label, value, t) = Items[i];
                float x = pad + i * slot + (slot - w) / 2;
                float bh = (float)(h * value / max);
                g.FillRectangle(bar, x, top + h - bh, w, Math.Max(1, bh));
                var ts = g.MeasureString(t, Font);
                if (value > 0) g.DrawString(t, Font, muted, x + w / 2 - ts.Width / 2, top + h - bh - ts.Height);
                var ls = g.MeasureString(label, Font);
                g.DrawString(label, Font, muted, x + w / 2 - ls.Width / 2, top + h + 4);
            }
        }
    }
}
