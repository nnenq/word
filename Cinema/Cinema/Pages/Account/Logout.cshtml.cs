using Cinema.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Pages.Account;

public class LogoutModel : AppPage
{
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Success("Вы вышли из системы.");
        return Redirect("/");
    }
}
