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
    public static string Day(DateTime d) =>
        d.Date == DateTime.Today ? "Сегодня" : d.Date == DateTime.Today.AddDays(1) ? "Завтра" : d.ToString("ddd, d MMMM", new System.Globalization.CultureInfo("ru-RU"));
    /// <summary>Цвет «постера» фильма (у демо-фильмов нет картинок).</summary>
    public static int Hue(int movieId) => movieId * 47 % 360;
}
