using System.Security.Claims;
using StudyCabinet.Data;
using StudyCabinet.Models;

namespace StudyCabinet.Services;

/// <summary>Журнал событий: изменение результатов, ручное открытие лекций, управление учётными записями, резервные копии.</summary>
public class Audit
{
    readonly AppDb db;
    readonly IHttpContextAccessor http;
    public Audit(AppDb db, IHttpContextAccessor http) { this.db = db; this.http = http; }

    public async Task WriteAsync(string action, string details)
    {
        var user = http.HttpContext?.User;
        db.AuditLog.Add(new AuditEntry
        {
            UserId = user?.UserId() is int id and > 0 ? id : null,
            UserName = user?.FullName() is { Length: > 0 } name ? name : "Система",
            Action = action,
            Details = details.Length > 2000 ? details[..2000] : details,
        });
        await db.SaveChangesAsync();
    }
}
