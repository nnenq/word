using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using StudyCabinet.Models;

namespace StudyCabinet.Pages.Account;

public static class Auth
{
    public static Task SignInAsync(HttpContext http, User user) =>
        http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("FullName", user.FullName),
        }, CookieAuthenticationDefaults.AuthenticationScheme)));

    /// <summary>Адрес возврата после входа — только внутри сайта.</summary>
    public static string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") ? url : "/";
}

/// <summary>Общие правила для логина, ФИО и пароля.</summary>
public static class UserRules
{
    public const string LoginPattern = @"^[a-zA-Z0-9_.\-]{3,50}$";
    public const string LoginMessage = "Логин: 3–50 латинских букв, цифр, точка, дефис или подчёркивание.";
    public const string NamePattern = @"^[А-Яа-яЁёA-Za-z\- ]{2,100}$";
    public const string NameMessage = "ФИО: только буквы, пробелы и дефис, от 2 символов.";
    public const string PasswordPattern = @"^(?=.*[A-Za-zА-Яа-яЁё])(?=.*\d).{8,100}$";
    public const string PasswordMessage = "Пароль: не короче 8 символов, хотя бы одна буква и одна цифра.";
}
