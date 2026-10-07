using System.ComponentModel.DataAnnotations;
using Cinema.Data;
using Cinema.Models;
using Cinema.Pages.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Pages.Cabinet;

public class ProfileInput
{
    [Required(ErrorMessage = "Введите ФИО.")]
    [RegularExpression(UserRules.NamePattern, ErrorMessage = UserRules.NameMessage)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Введите электронную почту.")]
    [EmailAddress(ErrorMessage = "Электронная почта указана неверно. Пример: name@mail.ru")]
    public string Email { get; set; } = "";
}

public class PasswordInput
{
    [Required(ErrorMessage = "Введите текущий пароль."), DataType(DataType.Password)]
    public string Current { get; set; } = "";

    [Required(ErrorMessage = "Введите новый пароль."), DataType(DataType.Password)]
    [RegularExpression(UserRules.PasswordPattern, ErrorMessage = UserRules.PasswordMessage)]
    public string New { get; set; } = "";

    [Required(ErrorMessage = "Повторите новый пароль."), DataType(DataType.Password)]
    [Compare(nameof(New), ErrorMessage = "Пароли не совпадают.")]
    public string Confirm { get; set; } = "";
}

/// <summary>Личный кабинет: мои билеты с возвратом, изменение данных и пароля.</summary>
public class IndexModel : AppPage
{
    readonly CinemaDb db;
    readonly TicketService tickets;
    readonly IPasswordHasher<User> hasher;
    public IndexModel(CinemaDb db, TicketService tickets, IPasswordHasher<User> hasher) { this.db = db; this.tickets = tickets; this.hasher = hasher; }

    public User Me { get; set; } = null!;
    public List<Ticket> Tickets { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "upcoming";
    [BindProperty] public ProfileInput Profile { get; set; } = new();
    [BindProperty] public PasswordInput Pass { get; set; } = new();

    async Task LoadAsync(bool fillProfile)
    {
        Me = await db.Users.Include(u => u.Role).FirstAsync(u => u.Id == User.UserId());
        if (fillProfile) Profile = new ProfileInput { FullName = Me.FullName, Email = Me.Email };
        var q = db.Tickets.Include(t => t.Session).ThenInclude(s => s.Movie).Include(t => t.Session).ThenInclude(s => s.Hall)
                  .Where(t => t.UserId == Me.Id);
        q = Show switch
        {
            "past" => q.Where(t => t.Status == TicketStatus.Paid && t.Session.StartTime <= DateTime.Now),
            "returned" => q.Where(t => t.Status == TicketStatus.Returned),
            "all" => q,
            _ => q.Where(t => t.Status == TicketStatus.Paid && t.Session.StartTime > DateTime.Now),
        };
        Tickets = await q.OrderBy(t => t.Session.StartTime).ThenBy(t => t.Row).ThenBy(t => t.Seat).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostReturnAsync(int id)
    {
        var me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        var error = await tickets.ReturnAsync(id, me, isStaff: false);
        if (error != null) Error(error); else Success("Билет возвращён. Деньги поступят на карту в течение 3 рабочих дней.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        ModelState.Clear();
        Profile.FullName = Profile.FullName?.Trim() ?? "";
        Profile.Email = Profile.Email?.Trim().ToLowerInvariant() ?? "";
        if (!TryValidateModel(Profile, nameof(Profile))) { await LoadAsync(false); return Page(); }
        var id = User.UserId();
        if (await db.Users.AnyAsync(u => u.Email == Profile.Email && u.Id != id))
        {
            ModelState.AddModelError("Profile.Email", "Эта почта уже используется другим пользователем.");
            await LoadAsync(false);
            return Page();
        }
        var me = await db.Users.Include(u => u.Role).FirstAsync(u => u.Id == id);
        me.FullName = Profile.FullName;
        me.Email = Profile.Email;
        await db.SaveChangesAsync();
        await Auth.SignInAsync(HttpContext, me); // обновить ФИО в шапке сайта
        Success("Данные профиля сохранены.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Pass, nameof(Pass))) { await LoadAsync(true); return Page(); }
        var me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        if (hasher.VerifyHashedPassword(me, me.PasswordHash, Pass.Current) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Pass.Current", "Текущий пароль введён неверно.");
            await LoadAsync(true);
            return Page();
        }
        me.PasswordHash = hasher.HashPassword(me, Pass.New);
        await db.SaveChangesAsync();
        Success("Пароль изменён.");
        return RedirectToPage();
    }
}
