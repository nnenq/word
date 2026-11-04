using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Журнал событий: изменения результатов, ручное открытие лекций, учётные записи, материалы, резервные копии.</summary>
public class JournalModel : AppPage
{
    readonly AppDb db;
    public JournalModel(AppDb db) => this.db = db;
    public string? Q { get; set; }
    public List<AuditEntry> Items { get; set; } = new();

    public async Task OnGetAsync(string? q)
    {
        Q = q;
        var list = await db.AuditLog.OrderByDescending(a => a.At).Take(1000).ToListAsync();
        if (!string.IsNullOrWhiteSpace(q)) list = list.Where(a => Grading.Normalize(a.Action + " " + a.Details + " " + a.UserName).Contains(Grading.Normalize(q))).ToList();
        Items = list.Take(300).ToList();
    }
}
