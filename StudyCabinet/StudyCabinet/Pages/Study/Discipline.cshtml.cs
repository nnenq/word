using Microsoft.AspNetCore.Mvc;
using StudyCabinet.Services;

namespace StudyCabinet.Pages.Study;

/// <summary>Дисциплина: темы и лекции с состоянием (пройдена, доступна, на проверке, закрыта) и общий процент освоения.</summary>
public class DisciplineModel : AppPage
{
    readonly StudyService study;
    public DisciplineModel(StudyService study) => this.study = study;

    public DisciplinePlan Plan { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var plan = await study.PlanAsync(id, User.UserId());
        if (plan == null) return NotFound();
        // Преподаватель видит дисциплину целиком
        if (User.IsTeacher()) foreach (var l in plan.Lectures.Where(l => l.State == LectureState.Locked)) l.State = LectureState.Available;
        Plan = plan;
        return Page();
    }
}
