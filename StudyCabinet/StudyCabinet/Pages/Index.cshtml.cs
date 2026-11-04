using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyCabinet.Services;

namespace StudyCabinet.Pages;

public class IndexModel : PageModel
{
    /// <summary>Вошедший пользователь сразу попадает в свой раздел.</summary>
    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? Redirect(User.IsTeacher() ? "/Teacher" : "/Study") : Page();
}
