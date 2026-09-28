using System.Text;
using ClosedXML.Excel;

namespace CrmSales;

/// <summary>Перенос клиентской базы из Excel/CSV со сверкой и объединением дублей, выгрузка в Excel.</summary>
public static class Importer
{
    public static readonly (string Key, string Title)[] Fields =
    {
        ("name", "Компания / ФИО"), ("contact", "Контактное лицо"), ("phone", "Телефон"), ("email", "Email"),
        ("inn", "ИНН"), ("city", "Город"), ("owner", "Менеджер"), ("note", "Комментарий"), ("skip", "— не переносить —"),
    };

    public static string FieldTitle(string key) => Fields.First(f => f.Key == key).Title;
    public static string FieldKey(string? title) => Fields.FirstOrDefault(f => f.Title == title).Key ?? "skip";

    public class Plan
    {
        public string FileName { get; set; } = "";
        public string[] Headers { get; set; } = Array.Empty<string>();
        public List<string[]> Rows { get; set; } = new();
        public string[] Map { get; set; } = Array.Empty<string>();
        public bool HasName => Map.Contains("name");
    }

    public static string Guess(string header)
    {
        var h = header.ToLowerInvariant();
        if (h.Contains("инн")) return "inn";
        if (h.Contains("тел") || h.Contains("phone") || h.Contains("моб")) return "phone";
        if (h.Contains("mail") || h.Contains("почт")) return "email";
        if (h.Contains("контакт") || h.Contains("лицо")) return "contact";
        if (h.Contains("город") || h.Contains("city")) return "city";
        if (h.Contains("менедж") || h.Contains("ответств")) return "owner";
        if (h.Contains("коммент") || h.Contains("примеч") || h.Contains("заметк")) return "note";
        if (new[] { "компан", "клиент", "наимен", "назван", "фио", "организ" }.Any(h.Contains)) return "name";
        return "skip";
    }

    public static Plan FromTable(string fileName, List<string[]> table)
    {
        table = table.Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v))).ToList();
        if (table.Count < 2) throw new InvalidDataException("в файле нет строк с данными");
        var headers = table[0].Select(h => h.Trim()).ToArray();
        var width = table.Max(r => r.Length);
        if (headers.Length < width) headers = headers.Concat(Enumerable.Range(headers.Length + 1, width - headers.Length).Select(i => "Столбец " + i)).ToArray();
        return new Plan { FileName = fileName, Headers = headers, Rows = table.Skip(1).ToList(), Map = headers.Select(Guess).ToArray() };
    }

    public static Plan Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        List<string[]> table;
        if (ext == ".csv" || ext == ".txt")
        {
            var bytes = File.ReadAllBytes(path);
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
            catch (DecoderFallbackException) { text = Encoding.GetEncoding(1251).GetString(bytes); } // CSV из старого Excel
            table = ParseCsv(text);
        }
        else if (ext == ".xlsx" || ext == ".xlsm")
        {
            using var wb = new XLWorkbook(path);
            var range = wb.Worksheet(1).RangeUsed() ?? throw new InvalidDataException("первый лист пуст");
            table = range.Rows().Select(r => r.Cells().Select(c => c.GetFormattedString()).ToArray()).ToList();
        }
        else throw new InvalidDataException("поддерживаются файлы .xlsx и .csv. Файл .xls пересохраните в Excel как .xlsx");
        return FromTable(Path.GetFileName(path), table);
    }

    public static List<string[]> ParseCsv(string text)
    {
        var firstLine = text.Split('\n')[0];
        char sep = firstLine.Count(c => c == ';') >= firstLine.Count(c => c == ',') ? ';' : ',';
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n') { row.Add(cell.ToString().TrimEnd('\r')); rows.Add(row.ToArray()); row.Clear(); cell.Clear(); }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }

    static Dictionary<string, string> RowValues(Plan p, string[] row)
    {
        var o = new Dictionary<string, string>();
        for (int i = 0; i < p.Map.Length; i++)
        {
            if (p.Map[i] == "skip" || i >= row.Length) continue;
            var v = (row[i] ?? "").Trim();
            if (v == "") continue;
            o[p.Map[i]] = o.TryGetValue(p.Map[i], out var prev) ? prev + "; " + v : v;
        }
        return o;
    }

    static Client ToClient(Dictionary<string, string> o) => new()
    {
        Name = o.GetValueOrDefault("name", ""), Contact = o.GetValueOrDefault("contact", ""),
        Phone = o.GetValueOrDefault("phone", ""), Email = o.GetValueOrDefault("email", ""),
        Inn = o.GetValueOrDefault("inn", ""), City = o.GetValueOrDefault("city", ""), Source = "Excel",
    };

    /// <summary>Предварительный расчёт: сколько строк станет новыми клиентами, сколько объединится, сколько пустых.</summary>
    public static (int Created, int Merged, int Empty) Analyze(Plan p)
    {
        int created = 0, merged = 0, empty = 0;
        var pool = Db.Data.Clients.ToList();
        foreach (var r in p.Rows)
        {
            var o = RowValues(p, r);
            if (!o.ContainsKey("name")) { empty++; continue; }
            var c = ToClient(o);
            if (Duplicates.Find(c, pool) != null) merged++;
            else { created++; pool.Add(c); }
        }
        return (created, merged, empty);
    }

    public static ImportRecord Run(Plan p)
    {
        Db.Backup($"Перед импортом «{p.FileName}»");
        var managers = Db.Managers.Where(u => u.Active).ToList();
        int created = 0, merged = 0, empty = 0, next = 0;
        foreach (var r in p.Rows)
        {
            var o = RowValues(p, r);
            if (!o.ContainsKey("name")) { empty++; continue; }
            var c = ToClient(o);
            var note = o.TryGetValue("note", out var n) ? ". Комментарий: " + n : "";
            var dup = Duplicates.Find(c, Db.Data.Clients);
            if (dup != null)
            {
                if (dup.Contact == "") dup.Contact = c.Contact;
                if (dup.Phone == "") dup.Phone = c.Phone;
                if (dup.Email == "") dup.Email = c.Email;
                if (dup.Inn == "") dup.Inn = c.Inn;
                if (dup.City == "") dup.City = c.City;
                dup.Updated = DateTime.Now;
                Db.AddHistory(dup.Id, InteractionType.System, $"Объединено с записью из файла «{p.FileName}»{note}");
                merged++;
            }
            else
            {
                var ownerName = o.GetValueOrDefault("owner", "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var owner = (ownerName == null ? null : managers.FirstOrDefault(m => m.Name.Contains(ownerName, StringComparison.OrdinalIgnoreCase)))
                            ?? (managers.Count > 0 ? managers[next++ % managers.Count] : Db.Current!);
                c.OwnerId = owner.Id;
                Db.Data.Clients.Add(c);
                Db.AddHistory(c.Id, InteractionType.System, $"Перенесён из файла «{p.FileName}»{note}");
                created++;
            }
        }
        var rec = new ImportRecord { FileName = p.FileName, Total = p.Rows.Count, Created = created, Merged = merged, Empty = empty };
        Db.Data.Imports.Insert(0, rec);
        var stage = Db.Data.Plan.FirstOrDefault(s => s.Id == "s5");
        if (stage != null) stage.Status = StageStatus.Done;
        Db.Log($"Импорт «{p.FileName}»: строк {rec.Total}, новых {created}, объединено {merged}, пустых {empty}");
        Db.Save();
        return rec;
    }

    /// <summary>Пример файла, который отдел вёл в Excel: новые клиенты, дубли существующих и пустая строка.</summary>
    public static Plan Demo() => FromTable("Клиенты_отдел_продаж.xlsx", new List<string[]>
    {
        new[] { "Наименование клиента", "Контактное лицо", "Тел. моб.", "E-mail", "ИНН", "Город", "Ответственный", "Примечание" },
        new[] { "ООО «Ромашка»", "Сергеева Инна", "8 (916) 100-20-30", "romashka@mail.ru", "7712000001", "Москва", "Петров", "Интересуются поставкой к весне" },
        new[] { "ООО «Гефест»", "Лаптев Борис", "+7 921 300-40-50", "gefest@yandex.ru", "7813000002", "Санкт-Петербург", "Иванова", "" },
        new[] { "ТехноПарк АО", "Лебедева С.", "", "ZAKAZ@TECHNOPARK.RU", "", "Санкт-Петербург", "Иванова", "Дубль: уже есть в CRM" },
        new[] { "ООО «Каскад»", "Жуков Пётр", "8-343-222-11-00", "kaskad@ural.ru", "6670000003", "Екатеринбург", "Кузнецов", "Просили перезвонить в понедельник" },
        new[] { "", "Без названия", "", "", "", "", "", "Строка без названия компании" },
        new[] { "ИП Тарасов", "Тарасов Егор", "89051112233", "tarasov.ip@gmail.com", "", "Воронеж", "Смирнова", "" },
        new[] { "ООО «Северный ветер»", "Андреева К.", "+7 8182 20 30 40", "", "", "Архангельск", "Орлов", "Дубль по телефону" },
        new[] { "ООО «НоваМед»", "Щербакова Юлия", "+7 (843) 555-00-99", "novamed@med.ru", "1655000004", "Казань", "Фёдорова", "" },
        new[] { "ООО «БетонПро»", "Рябов Кирилл", "+7 (863) 299-88-77", "", "6164000005", "Ростов-на-Дону", "Морозов", "Большой объём, нужен выезд" },
    });

    public static void ExportExcel(string path, IEnumerable<Client> clients, IEnumerable<Deal> deals)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Клиенты");
        string[] h1 = { "Компания", "Контактное лицо", "Телефон", "Email", "ИНН", "Город", "Источник", "Менеджер", "Создан" };
        for (int i = 0; i < h1.Length; i++) ws.Cell(1, i + 1).Value = h1[i];
        int r = 2;
        foreach (var c in clients)
        {
            object[] v = { c.Name, c.Contact, c.Phone, c.Email, c.Inn, c.City, c.Source, Db.UserName(c.OwnerId), c.Created.ToString("dd.MM.yyyy") };
            for (int i = 0; i < v.Length; i++) ws.Cell(r, i + 1).Value = v[i]?.ToString();
            r++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        var wd = wb.AddWorksheet("Заявки");
        string[] h2 = { "Клиент", "Заявка", "Сумма, ₽", "Статус", "Менеджер", "Создана", "Закрыта" };
        for (int i = 0; i < h2.Length; i++) wd.Cell(1, i + 1).Value = h2[i];
        r = 2;
        foreach (var d in deals)
        {
            wd.Cell(r, 1).Value = Db.ClientById(d.ClientId)?.Name ?? "";
            wd.Cell(r, 2).Value = d.Title;
            wd.Cell(r, 3).Value = d.Amount;
            wd.Cell(r, 4).Value = Names.Of(d.Status);
            wd.Cell(r, 5).Value = Db.UserName(d.OwnerId);
            wd.Cell(r, 6).Value = d.Created.ToString("dd.MM.yyyy");
            wd.Cell(r, 7).Value = d.Closed?.ToString("dd.MM.yyyy") ?? "";
            r++;
        }
        wd.Row(1).Style.Font.Bold = true;
        wd.Column(3).Style.NumberFormat.Format = "# ##0";
        wd.Columns().AdjustToContents();
        wb.SaveAs(path);
    }
}
