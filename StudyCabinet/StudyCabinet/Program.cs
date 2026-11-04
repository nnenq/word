using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // При запуске как служба Windows рабочая папка — папка программы
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
});
// Можно установить как службу Windows (см. deploy\install-service.bat); в обычном запуске ничего не меняет
builder.Host.UseWindowsService(o => o.ServiceName = "StudyCabinet");

// Журнал ошибок в файлы App_Data/logs
var logFolder = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Storage:Logs"] ?? "App_Data/logs"));
var fileLogs = new FileLoggerProvider(logFolder);
builder.Logging.AddProvider(fileLogs);
builder.Services.AddSingleton(fileLogs);

// База данных: SQLite (файл, ничего устанавливать не нужно) или SQL Server — выбирается в appsettings.json
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<AppDb>(o =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        o.UseSqlServer(builder.Configuration["Database:SqlServer"]);
    else
    {
        var cs = builder.Configuration["Database:Sqlite"] ?? "Data Source=App_Data/study.db";
        // Относительный путь к файлу базы — от папки приложения
        var file = cs.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!Path.IsPathRooted(file))
        {
            file = Path.Combine(builder.Environment.ContentRootPath, file);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            cs = $"Data Source={file}";
        }
        o.UseSqlite(cs);
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<StudyService>();
builder.Services.AddScoped<Audit>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddSingleton<MaterialStore>();
builder.Services.AddHostedService<AutoBackupService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Размер загружаемых лекций
var maxUpload = (builder.Configuration.GetValue<int?>("Upload:MaxMegabytes") ?? 50) * 1024L * 1024L;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUpload);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUpload + 1024 * 1024);

// Вход по cookie
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
        o.Cookie.Name = "StudyCabinet";
        // При каждом запросе проверяем, что пользователь существует, не заблокирован и роль не изменилась
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDb>();
            int id = ctx.Principal?.UserId() ?? 0;
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null || user.IsBlocked || user.Role != ctx.Principal?.FindFirstValue(ClaimTypes.Role))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Разграничение прав: роли «Преподаватель» и «Студент»
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Teacher", p => p.RequireRole(Roles.Teacher));
});
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Teacher", "Teacher");
    o.Conventions.AuthorizeFolder("/Study");
    o.Conventions.AuthorizeFolder("/Materials");
    o.Conventions.AuthorizePage("/Account/Profile");
});

var app = builder.Build();

// Запуск с ключом --sql-script сохраняет SQL-скрипт создания базы (для SQL Server: Database__Provider=SqlServer) и завершает работу
if (args.Contains("--sql-script"))
{
    using var scope = app.Services.CreateScope();
    var file = Path.GetFullPath($"create_database_{provider.ToLowerInvariant()}.sql");
    File.WriteAllText(file, scope.ServiceProvider.GetRequiredService<AppDb>().Database.GenerateCreateScript());
    Console.WriteLine("SQL-скрипт сохранён: " + file);
    return;
}

// Создание базы данных и начальные данные
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();
    await Seed.RunAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
        scope.ServiceProvider.GetRequiredService<MaterialStore>(), app.Environment.ContentRootPath);
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
// Базовые заголовки безопасности
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Проверка доступности для сопровождения: отвечает, работает ли сайт и подключается ли база
app.MapGet("/health", async (AppDb db) =>
{
    bool ok;
    try { ok = await db.Database.CanConnectAsync(); } catch { ok = false; }
    return Results.Json(new { status = ok ? "ok" : "db-error", database = db.Database.ProviderName, time = DateTime.Now },
        statusCode: ok ? 200 : 503);
});

app.Run();
