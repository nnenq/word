using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cinema.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public int? Code { get; set; }
    public void OnGet(int? code) => Code = code;
    public void OnPost(int? code) => Code = code;
}
