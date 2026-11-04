using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyCabinet.Models;

namespace StudyCabinet.Services;

public static class UserExtensions
{
    public static int UserId(this ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public static bool IsTeacher(this ClaimsPrincipal p) => p.IsInRole(Roles.Teacher);
    public static bool IsStudent(this ClaimsPrincipal p) => p.IsInRole(Roles.Student);
    public static string FullName(this ClaimsPrincipal p) => p.FindFirstValue("FullName") ?? p.Identity?.Name ?? "";
}

/// <summary>Базовая страница: сообщения об успехе и ошибке после перенаправления.</summary>
public abstract class AppPage : PageModel
{
    protected void Success(string text) => TempData["Success"] = text;
    protected void Error(string text) => TempData["Error"] = text;
}

public static class Fmt
{
    static readonly CultureInfo Ru = new("ru-RU");
    public static string Date(DateTime d) => d.ToString("dd.MM.yyyy", Ru);
    public static string DateTime(DateTime d) => d.ToString("dd.MM.yyyy HH:mm", Ru);
    public static string DateTime(DateTime? d) => d == null ? "—" : DateTime(d.Value);
    public static string Plural(int n, string one, string few, string many) =>
        (n % 100 is >= 11 and <= 14) ? many : (n % 10) switch { 1 => one, >= 2 and <= 4 => few, _ => many };
    public static string Size(long bytes) => bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} КБ" : $"{bytes / 1024.0 / 1024:0.#} МБ";
}

/// <summary>
/// Простая разметка теории лекции: «# » и «## » — заголовки, «- » — список, пустая строка — новый абзац,
/// **жирный**, `код`. Весь текст сначала экранируется, поэтому вставить HTML-код в лекцию нельзя.
/// </summary>
public static class Markup
{
    public static string ToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        var para = new List<string>();
        bool inList = false;
        void FlushPara() { if (para.Count > 0) { sb.Append("<p>").Append(string.Join("<br>", para)).Append("</p>"); para.Clear(); } }
        void CloseList() { if (inList) { sb.Append("</ul>"); inList = false; } }
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { FlushPara(); CloseList(); continue; }
            if (line.StartsWith("## ")) { FlushPara(); CloseList(); sb.Append("<h4>").Append(Inline(line[3..])).Append("</h4>"); }
            else if (line.StartsWith("# ")) { FlushPara(); CloseList(); sb.Append("<h3>").Append(Inline(line[2..])).Append("</h3>"); }
            else if (line.StartsWith("- ")) { FlushPara(); if (!inList) { sb.Append("<ul>"); inList = true; } sb.Append("<li>").Append(Inline(line[2..])).Append("</li>"); }
            else { CloseList(); para.Add(Inline(line)); }
        }
        FlushPara(); CloseList();
        return sb.ToString();
    }

    static string Inline(string s)
    {
        var e = WebUtility.HtmlEncode(s);
        e = Regex.Replace(e, @"\*\*(.+?)\*\*", "<b>$1</b>");
        e = Regex.Replace(e, @"`(.+?)`", "<code>$1</code>");
        return e;
    }
}
