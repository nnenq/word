using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StudyCabinet.Pages.Account;

public class AccessDeniedModel : PageModel
{
    public string? Reason { get; set; }
    public void OnGet() { }
}
