using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyCabinet.Data;
using StudyCabinet.Services;
using M = StudyCabinet.Services.MaterialStore;

namespace StudyCabinet.Pages.Materials;

/// <summary>
/// Выдача файла лекции. Только вошедшим пользователям и только если лекция открыта студенту —
/// по прямой ссылке закрытый материал не скачать. Кэширование отключено, чтобы после замены файла
/// студент сразу получал новую версию.
/// </summary>
public class IndexModel : PageModel
{
    readonly AppDb db;
    readonly StudyService study;
    readonly M materials;
    public IndexModel(AppDb db, StudyService study, M materials) { this.db = db; this.study = study; this.materials = materials; }

    public async Task<IActionResult> OnGetAsync(int id, bool download = false)
    {
        var l = await db.Lectures.FindAsync(id);
        if (l == null) return NotFound();
        if (!await study.CanOpenAsync(id, User.UserId(), User.IsTeacher())) return Forbid();
        if (!materials.Exists(l)) return NotFound();
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        var inline = !download && (M.IsPdf(l.MaterialName) || M.IsText(l.MaterialName));
        var stream = System.IO.File.OpenRead(materials.PathOf(l));
        return inline
            ? File(stream, M.ContentType(l.MaterialName))
            : File(stream, M.ContentType(l.MaterialName), l.MaterialName);
    }
}
