using System.Globalization;
using System.Security.Claims;
using Cinema.Data;
using Cinema.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// База данных SQLite: файл cinema.db в папке проекта
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "cinema.db");
builder.Services.AddDbContext<CinemaDb>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Авторизация по cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // При каждом запросе проверяем, что пользователь не удалён, не заблокирован и роль не изменилась
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<CinemaDb>();
            var id = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var x) ? x : 0;
            var user = await db.Users.Include(u => u.Role).AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null || user.IsBlocked || user.Role.Code != ctx.Principal?.FindFirstValue(ClaimTypes.Role))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Разграничение доступа: политики по ролям
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Admin", p => p.RequireRole(RoleCodes.Admin));
    o.AddPolicy("Staff", p => p.RequireRole(RoleCodes.Admin, RoleCodes.Cashier));
});

// Защита страниц: целые разделы закрыты для неавторизованных и для пользователей без нужной роли
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", "Admin");
    o.Conventions.AuthorizeFolder("/Cashier", "Staff");
    o.Conventions.AuthorizeFolder("/Cabinet");
    o.Conventions.AuthorizeFolder("/Tickets");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CinemaDb>();
    db.Database.EnsureCreated();
    Seed.Run(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());
}

var ru = new CultureInfo("ru-RU");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ru),
    SupportedCultures = new[] { ru },
    SupportedUICultures = new[] { ru },
});

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
