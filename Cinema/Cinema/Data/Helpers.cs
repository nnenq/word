using System.Security.Claims;
using Cinema.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cinema.Data;

public static class UserExtensions
{
    public static int? UserId(this ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsAdmin(this ClaimsPrincipal p) => p.IsInRole(RoleCodes.Admin);
    public static bool IsCashier(this ClaimsPrincipal p) => p.IsInRole(RoleCodes.Cashier);
    public static bool IsStaff(this ClaimsPrincipal p) => p.IsAdmin() || p.IsCashier();
    public static string FullName(this ClaimsPrincipal p) => p.FindFirstValue("FullName") ?? p.Identity?.Name ?? "";
    public static string RoleName(this ClaimsPrincipal p) => p.FindFirstValue("RoleName") ?? "";
}

/// <summary>Базовая страница: сообщения об успехе и ошибке, которые показываются после перенаправления.</summary>
public abstract class AppPage : PageModel
{
    protected void Success(string text) => TempData["Success"] = text;
    protected void Error(string text) => TempData["Error"] = text;

    /// <summary>Номера билетов из формы: «12,13,14» или несколько значений поля.</summary>
    protected static List<int> ParseIds(IEnumerable<string?> values) =>
        values.SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();

    /// <summary>Слово в нужной форме: 1 билет, 2 билета, 5 билетов.</summary>
    public static string Plural(int n, string one, string few, string many) =>
        (n % 100 is >= 11 and <= 14) ? many : (n % 10) switch { 1 => one, >= 2 and <= 4 => few, _ => many };
}

public static class Search
{
    /// <summary>Есть ли строка поиска хотя бы в одном из полей (без учёта регистра, «е» и «ё» равны).</summary>
    public static bool Match(string? query, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        static string N(string? s) => (s ?? "").ToLowerInvariant().Replace('ё', 'е');
        var q = N(query.Trim());
        return fields.Any(f => N(f).Contains(q));
    }
}

public static class Fmt
{
    public static string Money(int rub) => rub.ToString("N0", new System.Globalization.CultureInfo("ru-RU")) + " ₽";
    public static string Duration(int min) => $"{min / 60} ч {min % 60:D2} мин";
    /// <summary>Длительность в виде часов, например 2:22 — как на превью видео.</summary>
    public static string Clock(int min) => $"{min / 60}:{min % 60:D2}";
    public static string Day(DateTime d) =>
        d.Date == DateTime.Today ? "Сегодня" : d.Date == DateTime.Today.AddDays(1) ? "Завтра" : d.ToString("ddd, d MMMM", new System.Globalization.CultureInfo("ru-RU"));
    /// <summary>Цвет заглушки постера, если у фильма нет картинки.</summary>
    public static int Hue(int movieId) => movieId * 47 % 360;
}
