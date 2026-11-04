using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Data;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Teacher;

/// <summary>
/// Отчёт по дисциплине и группе: таблица «студенты × лекции» с состоянием, лучшим результатом теста и попытками.
/// Фильтр «не завершили тему» показывает отстающих. Выгрузка в CSV открывается в Excel.
/// </summary>
public class ReportModel : AppPage
{
    readonly AppDb db;
    readonly StudyService study;
    public ReportModel(AppDb db, StudyService study) { this.db = db; this.study = study; }

    [BindProperty(SupportsGet = true)] public int? Discipline { get; set; }
    [BindProperty(SupportsGet = true)] public int? Group { get; set; }
    [BindProperty(SupportsGet = true)] public int? Topic { get; set; }

    public List<Discipline> Disciplines { get; set; } = new();
    public List<StudentGroup> Groups { get; set; } = new();
    public List<Topic> Topics { get; set; } = new();
    public List<Lecture> Lectures { get; set; } = new();
    public List<(User U, DisciplinePlan Plan)> Rows { get; set; } = new();
    public Discipline? Current { get; set; }

    async Task LoadAsync()
    {
        Disciplines = await db.Disciplines.OrderBy(d => d.Title).ToListAsync();
        Groups = await db.StudentGroups.OrderBy(g => g.Name).ToListAsync();
        Current = Disciplines.FirstOrDefault(d => d.Id == Discipline) ?? Disciplines.FirstOrDefault();
        if (Current == null) return;
        Discipline = Current.Id;
        Topics = await db.Topics.Where(t => t.DisciplineId == Current.Id).OrderBy(t => t.Order).ToListAsync();
        Lectures = await study.OrderedLecturesAsync(Current.Id);
        var students = await db.Users.Where(u => u.Role == Roles.Student && (Group == null || u.Memberships.Any(m => m.GroupId == Group)))
            .OrderBy(u => u.FullName).ToListAsync();
        foreach (var u in students)
        {
            var plan = (await study.PlanAsync(Current.Id, u.Id))!;
            // Фильтр: студенты, не завершившие выбранную тему
            if (Topic != null && plan.Lectures.Where(l => l.Topic.Id == Topic).All(l => l.State == LectureState.Completed)) continue;
            Rows.Add((u, plan));
        }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetCsvAsync()
    {
        await LoadAsync();
        if (Current == null) return NotFound();
        var groupNames = await db.GroupMemberships.Include(m => m.Group).ToListAsync();
        var sb = new StringBuilder();
        string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        sb.AppendLine(string.Join(";", new[] { "ФИО", "Группа" }.Concat(Lectures.Select(l => $"Лекция {l.Number}")).Append("Освоение, %").Select(Q)));
        foreach (var (u, plan) in Rows)
        {
            var cells = plan.Lectures.Select(l => l.State switch
            {
                LectureState.Completed => "пройдена" + (l.BestPercent != null ? $" ({l.BestPercent}%)" : ""),
                LectureState.Pending => "на проверке",
                LectureState.Available => l.AttemptsUsed > 0 ? $"тест {l.BestPercent}%, попыток {l.AttemptsUsed}" : "изучает",
                _ => "закрыта",
            });
            var g = string.Join(", ", groupNames.Where(m => m.UserId == u.Id).Select(m => m.Group.Name));
            sb.AppendLine(string.Join(";", new[] { u.FullName, g }.Concat(cells).Append(plan.Percent.ToString()).Select(Q)));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"Отчёт_{Current.Title}_{DateTime.Now:yyyy-MM-dd}.csv");
    }
}
