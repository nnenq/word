using System.IO;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UsersApp.Data;

// Модель данных, построенная по базе данных UsersDB (таблицы «Пользователь» и «СекретныйВопрос»).
// Имена классов и свойств совпадают с именами таблиц и столбцов в MS SQL Server.

[Table("Пользователь")]
public partial class Пользователь
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int КодПользователя { get; set; }

    [MaxLength(50)] public string Фамилия { get; set; } = "";
    [MaxLength(50)] public string Имя { get; set; } = "";
    [MaxLength(100)] public string ЭлектроннаяПочта { get; set; } = "";
    /// <summary>Хеш пароля (SHA-256), сам пароль в базе не хранится.</summary>
    [MaxLength(100)] public string Пароль { get; set; } = "";
    [MaxLength(50)] public string КодовоеСлово { get; set; } = "";
    /// <summary>Хеш ответа на секретный вопрос.</summary>
    [MaxLength(100)] public string ОтветНаСекретныйВопрос { get; set; } = "";
    public int КодСекретногоВопроса { get; set; }
    public DateTime ДатаРегистрации { get; set; }

    [ForeignKey(nameof(КодСекретногоВопроса))]
    public virtual СекретныйВопрос СекретныйВопрос { get; set; } = null!;
}

[Table("СекретныйВопрос")]
public partial class СекретныйВопрос
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int КодСекретногоВопроса { get; set; }

    /// <summary>Текст вопроса (столбец «СекретныйВопрос»; в C# свойство не может называться как класс).</summary>
    [Column("СекретныйВопрос"), MaxLength(100)]
    public string СекретныйВопрос1 { get; set; } = "";

    public virtual ICollection<Пользователь> Пользователь { get; set; } = new List<Пользователь>();
}

/// <summary>Контекст данных Entity Framework: через него приложение читает и сохраняет данные в MS SQL Server.</summary>
public partial class UsersContext : DbContext
{
    public UsersContext() { }
    public UsersContext(DbContextOptions<UsersContext> options) : base(options) { }

    public virtual DbSet<Пользователь> Пользователь { get; set; } = null!;
    public virtual DbSet<СекретныйВопрос> СекретныйВопрос { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer(ConnectionSettings.ConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Пользователь>().HasIndex(u => u.ЭлектроннаяПочта).IsUnique();
        b.Entity<Пользователь>().Property(u => u.ДатаРегистрации).HasDefaultValueSql("SYSDATETIME()");
        b.Entity<Пользователь>().HasOne(u => u.СекретныйВопрос).WithMany(q => q.Пользователь)
            .HasForeignKey(u => u.КодСекретногоВопроса).OnDelete(DeleteBehavior.Restrict);
        b.Entity<СекретныйВопрос>().HasIndex(q => q.СекретныйВопрос1).IsUnique();
    }
}

/// <summary>Строка подключения к MS SQL Server. Имя сервера можно поменять в окне входа (сохраняется в connection.txt).</summary>
public static class ConnectionSettings
{
    public const string DefaultServer = @"(localdb)\MSSQLLocalDB";
    public const string Database = "UsersDB";
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "connection.txt");

    public static string Server
    {
        get
        {
            try { return File.Exists(FilePath) && File.ReadAllText(FilePath).Trim() is { Length: > 0 } s ? s : DefaultServer; }
            catch (IOException) { return DefaultServer; }
        }
        set => File.WriteAllText(FilePath, value.Trim());
    }

    /// <summary>Вход через учётную запись Windows (как при подключении в SSMS).</summary>
    public static string Build(string server) =>
        $"Server={server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

    public static string ConnectionString => Build(Server);
}
