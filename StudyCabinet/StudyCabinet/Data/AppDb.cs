using Microsoft.EntityFrameworkCore;
using StudyCabinet.Models;

namespace StudyCabinet.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<ControlQuestion> ControlQuestions => Set<ControlQuestion>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();
    public DbSet<TestAttempt> TestAttempts => Set<TestAttempt>();
    public DbSet<LectureProgress> LectureProgress => Set<LectureProgress>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Login).IsUnique();
        b.Entity<StudentGroup>().HasIndex(g => g.Name).IsUnique();
        b.Entity<GroupMembership>().HasIndex(m => new { m.UserId, m.GroupId }).IsUnique();

        b.Entity<Topic>().HasOne(t => t.Discipline).WithMany(d => d.Topics).HasForeignKey(t => t.DisciplineId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Lecture>().HasOne(l => l.Topic).WithMany(t => t.Lectures).HasForeignKey(l => l.TopicId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Test>().HasOne(t => t.Lecture).WithOne(l => l.Test).HasForeignKey<Test>(t => t.LectureId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Test>().HasIndex(t => t.LectureId).IsUnique();

        // Вариант ответа принадлежит либо контрольному вопросу, либо вопросу теста.
        // Второй путь каскадного удаления в SQL Server недопустим, поэтому варианты вопросов теста удаляются в коде (Cleanup).
        b.Entity<AnswerOption>().HasOne(o => o.ControlQuestion).WithMany(q => q.Options).HasForeignKey(o => o.ControlQuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AnswerOption>().HasOne(o => o.TestQuestion).WithMany(q => q.Options).HasForeignKey(o => o.TestQuestionId).OnDelete(DeleteBehavior.ClientCascade);
        b.Entity<AnswerOption>().ToTable(t => t.HasCheckConstraint("CK_AnswerOption_Owner",
            "(ControlQuestionId IS NOT NULL AND TestQuestionId IS NULL) OR (ControlQuestionId IS NULL AND TestQuestionId IS NOT NULL)"));

        b.Entity<StudentAnswer>().HasIndex(a => new { a.StudentId, a.ControlQuestionId }).IsUnique();
        b.Entity<LectureProgress>().HasIndex(p => new { p.StudentId, p.LectureId }).IsUnique();
        b.Entity<TestAttempt>().HasIndex(a => new { a.StudentId, a.TestId });
        b.Entity<AuditEntry>().HasIndex(a => a.At);

        b.Entity<Test>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Test_PassPercent", "PassPercent BETWEEN 1 AND 100");
            t.HasCheckConstraint("CK_Test_Attempts", "MaxAttempts >= 0");
        });
    }

    /// <summary>
    /// Перед удалением лекций (дисциплины, темы, лекции, теста) удаляет варианты ответов вопросов теста —
    /// их база не удаляет каскадом (см. выше).
    /// </summary>
    public Task CleanupTestOptionsAsync(IQueryable<int> testIds) =>
        AnswerOptions.Where(o => o.TestQuestionId != null && testIds.Contains(o.TestQuestion!.TestId)).ExecuteDeleteAsync();
}
