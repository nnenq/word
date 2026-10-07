using System.Security.Claims;
using Cinema.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Cinema.Pages.Account;

public static class Auth
{
    /// <summary>Выдаёт пользователю cookie входа с его Id, логином, ФИО и ролью.</summary>
    public static Task SignInAsync(HttpContext http, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Login),
            new(ClaimTypes.Role, user.Role.Code),
            new("FullName", user.FullName),
            new("RoleName", user.Role.Name),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>Безопасный адрес возврата после входа: только внутри сайта.</summary>
    public static string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") ? url : "/";
}
