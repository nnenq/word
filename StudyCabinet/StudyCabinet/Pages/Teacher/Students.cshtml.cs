using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>Список студентов с прогрессом по каждой дисциплине; поиск и фильтр по группе.</summary>
public class StudentsModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    public StudentsModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    [BindProperty(SupportsGet = true)] public int? Group { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public List<StudentGroup> Groups { get; set; } = new();
    public List<Discipline> Disciplines { get; set; } = new();
    public List<(User U, string? Group, List<DisciplinePlan> Plans)> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        Groups = await db.StudentGroups.OrderBy(g => g.Name).ToListAsync();
        Disciplines = await db.Disciplines.OrderBy(d => d.Title).ToListAsync();
        var users = await db.Users.Include(u => u.Memberships).ThenInclude(m => m.Group)
            .Where(u => u.Role == Roles.Student && (Group == null || u.Memberships.Any(m => m.GroupId == Group))).OrderBy(u => u.FullName).ToListAsync();
        if (!string.IsNullOrWhiteSpace(Q)) users = users.Where(u => Grading.Normalize(u.FullName + " " + u.Login).Contains(Grading.Normalize(Q))).ToList();
        foreach (var u in users)
        {
            var plans = new List<DisciplinePlan>();
            foreach (var d in Disciplines) plans.Add((await study.PlanAsync(d.Id, u.Id))!);
            Rows.Add((u, string.Join(", ", u.Memberships.Select(m => m.Group.Name)), plans));
        }
    }
}
