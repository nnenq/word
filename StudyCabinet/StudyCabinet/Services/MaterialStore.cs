using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;
using StudyCabinet.Models;

namespace StudyCabinet.Services;

/// <summary>
/// Хранение файлов лекций. Файлы лежат в отдельной папке вне wwwroot, поэтому их нельзя скачать по прямой ссылке:
/// выдача идёт только через страницу /Materials/{id}, которая проверяет права (см. Pages/Materials).
/// </summary>
public class MaterialStore
{
    public static readonly string[] Allowed = { ".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls", ".txt", ".odt", ".rtf", ".zip" };
    public string Folder { get; }
    public long MaxBytes { get; }

    public MaterialStore(IConfiguration config, IWebHostEnvironment env)
    {
        Folder = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Materials"] ?? "App_Data/materials"));
        Directory.CreateDirectory(Folder);
        MaxBytes = (config.GetValue<int?>("Upload:MaxMegabytes") ?? 50) * 1024L * 1024L;
    }

    public string PathOf(Lecture l) => Path.Combine(Folder, l.MaterialFile ?? "");
    public bool Exists(Lecture l) => l.MaterialFile != null && File.Exists(PathOf(l));

    /// <summary>Проверка файла перед загрузкой. null — всё хорошо, иначе текст ошибки.</summary>
    public string? Validate(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) return $"Формат {ext} не поддерживается. Можно: {string.Join(", ", Allowed)}.";
        if (file.Length == 0) return "Файл пустой.";
        if (file.Length > MaxBytes) return $"Файл больше {MaxBytes / 1024 / 1024} МБ.";
        return null;
    }

    /// <summary>Сохраняет новый файл лекции (или заменяет старый). Версия материала увеличивается.</summary>
    public async Task SaveAsync(Lecture l, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var name = $"lecture{l.Id}_{Guid.NewGuid():N}{ext}";
        await using (var fs = File.Create(Path.Combine(Folder, name)))
            await file.CopyToAsync(fs);
        Delete(l);
        l.MaterialFile = name;
        l.MaterialName = Path.GetFileName(file.FileName);
        l.MaterialUpdatedAt = DateTime.Now;
        l.MaterialVersion++;
    }

    /// <summary>Копирует файл с диска (для демонстрационных данных).</summary>
    public void Import(Lecture l, string source)
    {
        var name = $"lecture{l.Id}_{Guid.NewGuid():N}{Path.GetExtension(source).ToLowerInvariant()}";
        File.Copy(source, Path.Combine(Folder, name));
        l.MaterialFile = name;
        l.MaterialName = Path.GetFileName(source);
        l.MaterialUpdatedAt = DateTime.Now;
        l.MaterialVersion = 1;
    }

    public void Delete(Lecture l)
    {
        if (l.MaterialFile != null && File.Exists(PathOf(l))) File.Delete(PathOf(l));
    }

    public static string ContentType(string? fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".doc" => "application/msword",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls" => "application/vnd.ms-excel",
        ".txt" => "text/plain; charset=utf-8",
        ".odt" => "application/vnd.oasis.opendocument.text",
        ".rtf" => "application/rtf",
        _ => "application/octet-stream",
    };

    public static bool IsPdf(string? name) => Path.GetExtension(name ?? "").Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    public static bool IsDocx(string? name) => Path.GetExtension(name ?? "").Equals(".docx", StringComparison.OrdinalIgnoreCase);
    public static bool IsText(string? name) => Path.GetExtension(name ?? "").Equals(".txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Просмотр документа Word прямо в приложении: текст абзацев, заголовки, списки, жирный шрифт и таблицы.
    /// Картинки и сложное оформление не переносятся — для них есть кнопка «Скачать».
    /// </summary>
    public string DocxToHtml(Lecture l)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        using var zip = ZipFile.OpenRead(PathOf(l));
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) return "";
        XDocument doc;
        using (var s = entry.Open()) doc = XDocument.Load(s);
        var body = doc.Root?.Element(w + "body");
        if (body == null) return "";
        var sb = new StringBuilder();

        string Runs(XElement p) => string.Concat(p.Descendants(w + "r").Select(r =>
        {
            var t = WebUtility.HtmlEncode(string.Concat(r.Elements().Select(e => e.Name == w + "t" ? e.Value : e.Name == w + "tab" ? " " : e.Name == w + "br" ? "\n" : "")));
            t = t.Replace("\n", "<br>");
            var rpr = r.Element(w + "rPr");
            if (t.Length > 0 && rpr?.Element(w + "b") is XElement bEl && bEl.Attribute(w + "val")?.Value is null or "1" or "true") t = $"<b>{t}</b>";
            if (t.Length > 0 && rpr?.Element(w + "i") != null) t = $"<i>{t}</i>";
            return t;
        }));

        bool inList = false;
        foreach (var el in body.Elements())
        {
            if (el.Name == w + "p")
            {
                var style = el.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value ?? "";
                bool list = el.Element(w + "pPr")?.Element(w + "numPr") != null || style.StartsWith("List", StringComparison.OrdinalIgnoreCase);
                var html = Runs(el);
                if (!list && inList) { sb.Append("</ul>"); inList = false; }
                if (html.Trim().Length == 0) continue;
                if (list) { if (!inList) { sb.Append("<ul>"); inList = true; } sb.Append("<li>").Append(html).Append("</li>"); }
                else if (style.StartsWith("Heading1") || style == "Title" || style == "1") sb.Append("<h3>").Append(html).Append("</h3>");
                else if (style.StartsWith("Heading") || style is "2" or "3") sb.Append("<h4>").Append(html).Append("</h4>");
                else sb.Append("<p>").Append(html).Append("</p>");
            }
            else if (el.Name == w + "tbl")
            {
                if (inList) { sb.Append("</ul>"); inList = false; }
                sb.Append("<table class=\"table table-bordered table-sm\">");
                foreach (var tr in el.Elements(w + "tr"))
                {
                    sb.Append("<tr>");
                    foreach (var tc in tr.Elements(w + "tc"))
                        sb.Append("<td>").Append(string.Join("<br>", tc.Elements(w + "p").Select(Runs))).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
        }
        if (inList) sb.Append("</ul>");
        return sb.ToString();
    }
}
