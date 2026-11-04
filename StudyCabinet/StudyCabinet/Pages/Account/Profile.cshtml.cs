using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Account;

public class ProfileModel : AppPage
{
    readonly AppDb db;
    readonly IPasswordHasher<User> hasher;
    public ProfileModel(AppDb db, IPasswordHasher<User> hasher) { this.db = db; this.hasher = hasher; }

    public User Me { get; set; } = null!;
    public List<string> Groups { get; set; } = new();
    [BindProperty, Required(ErrorMessage = "Введите текущий пароль.")] public string Current { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Введите новый пароль."), RegularExpression(UserRules.PasswordPattern, ErrorMessage = UserRules.PasswordMessage)]
    public string New { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Повторите пароль."), Compare(nameof(New), ErrorMessage = "Пароли не совпадают.")]
    public string Confirm { get; set; } = "";

    async Task LoadAsync()
    {
        Me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        Groups = await db.GroupMemberships.Where(m => m.UserId == Me.Id).Select(m => m.Group.Name).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();
        if (hasher.VerifyHashedPassword(Me, Me.PasswordHash, Current) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(Current), "Текущий пароль введён неверно.");
            return Page();
        }
        Me.PasswordHash = hasher.HashPassword(Me, New);
        await db.SaveChangesAsync();
        Success("Пароль изменён.");
        return RedirectToPage();
    }
}
