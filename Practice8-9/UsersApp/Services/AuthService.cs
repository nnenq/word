using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UsersApp.Data;

namespace UsersApp.Services;

/// <summary>Данные, введённые в окне регистрации.</summary>
public class RegistrationInput
{
    public string Фамилия { get; set; } = "";
    public string Имя { get; set; } = "";
    public string ЭлектроннаяПочта { get; set; } = "";
    public string Пароль { get; set; } = "";
    public string ПовторПароля { get; set; } = "";
    public string КодовоеСлово { get; set; } = "";
    public int? КодСекретногоВопроса { get; set; }
    public string Ответ { get; set; } = "";
    public bool СогласенСУсловиями { get; set; }
}

/// <summary>Регистрация, вход и восстановление пароля. Не зависит от окон, поэтому проверяется автотестом.</summary>
public class AuthService
{
    readonly UsersContext context;
    public AuthService(UsersContext context) => this.context = context;

    // ---------- Правила проверки ----------

    static readonly Regex NameRx = new(@"^[A-Za-zА-Яа-яЁё][A-Za-zА-Яа-яЁё\-]{1,49}$");
    static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$");
    static readonly Regex WordRx = new(@"^[A-Za-zА-Яа-яЁё0-9]{3,30}$");

    /// <summary>Четыре условия пароля, которые показываются в окне регистрации (выполнено или нет).</summary>
    public static (string Text, bool Ok)[] PasswordConditions(string password) => new[]
    {
        ("Не менее 6 символов", password.Length >= 6),
        ("Есть заглавная буква", password.Any(char.IsUpper)),
        ("Есть строчная буква", password.Any(char.IsLower)),
        ("Есть цифра", password.Any(char.IsDigit)),
    };

    /// <summary>Проверяет все поля формы. Ключ — имя поля, значение — текст ошибки.</summary>
    public Dictionary<string, string> Validate(RegistrationInput i)
    {
        var e = new Dictionary<string, string>();
        if (!NameRx.IsMatch(i.Фамилия.Trim())) e["Фамилия"] = "Фамилия: от 2 до 50 букв, допускается дефис.";
        if (!NameRx.IsMatch(i.Имя.Trim())) e["Имя"] = "Имя: от 2 до 50 букв, допускается дефис.";
        var email = i.ЭлектроннаяПочта.Trim().ToLowerInvariant();
        if (!EmailRx.IsMatch(email)) e["ЭлектроннаяПочта"] = "E-mail указан неверно. Пример: ivanov@gmail.com";
        else if (context.Пользователь.Any(u => u.ЭлектроннаяПочта == email)) e["ЭлектроннаяПочта"] = "Пользователь с таким e-mail уже зарегистрирован.";
        if (!PasswordConditions(i.Пароль).All(c => c.Ok)) e["Пароль"] = "Пароль не соответствует условиям.";
        else if (i.Пароль != i.ПовторПароля) e["ПовторПароля"] = "Пароли не совпадают.";
        if (!WordRx.IsMatch(i.КодовоеСлово.Trim())) e["КодовоеСлово"] = "Кодовое слово: одно слово из 3–30 букв или цифр, без пробелов.";
        if (i.КодСекретногоВопроса is not int q || !context.СекретныйВопрос.Any(x => x.КодСекретногоВопроса == q))
            e["СекретныйВопрос"] = "Выберите секретный вопрос.";
        if (i.Ответ.Trim().Length < 2) e["Ответ"] = "Введите ответ на секретный вопрос.";
        if (!i.СогласенСУсловиями) e["Согласие"] = "Нужно согласиться с условиями.";
        return e;
    }

    // ---------- Хеширование ----------

    /// <summary>Хеш SHA-256 (64 символа). Почта добавляется как «соль», чтобы одинаковые пароли давали разный хеш.</summary>
    public static string Hash(string email, string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant() + ":" + secret)));

    static string NormalizeAnswer(string answer) => answer.Trim().ToLowerInvariant().Replace('ё', 'е');

    // ---------- Регистрация, вход, восстановление ----------

    /// <summary>Создаёт пользователя и сохраняет его в базе данных. Возвращает пользователя или ошибки.</summary>
    public (Пользователь? User, Dictionary<string, string> Errors) Register(RegistrationInput i)
    {
        var errors = Validate(i);
        if (errors.Count > 0) return (null, errors);
        var email = i.ЭлектроннаяПочта.Trim().ToLowerInvariant();
        var user = new Пользователь
        {
            Фамилия = i.Фамилия.Trim(),
            Имя = i.Имя.Trim(),
            ЭлектроннаяПочта = email,
            Пароль = Hash(email, i.Пароль),
            КодовоеСлово = i.КодовоеСлово.Trim(),
            ОтветНаСекретныйВопрос = Hash(email, NormalizeAnswer(i.Ответ)),
            КодСекретногоВопроса = i.КодСекретногоВопроса!.Value,
        };
        context.Пользователь.Add(user);
        context.SaveChanges();
        return (user, errors);
    }

    /// <summary>Вход по e-mail и паролю. Возвращает пользователя или null.</summary>
    public Пользователь? Login(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var user = context.Пользователь.Include(u => u.СекретныйВопрос).FirstOrDefault(u => u.ЭлектроннаяПочта == email);
        return user != null && user.Пароль == Hash(email, password) ? user : null;
    }

    /// <summary>Секретный вопрос пользователя для восстановления пароля (null — пользователь не найден).</summary>
    public string? QuestionFor(string email)
    {
        email = email.Trim().ToLowerInvariant();
        return context.Пользователь.Where(u => u.ЭлектроннаяПочта == email).Select(u => u.СекретныйВопрос.СекретныйВопрос1).FirstOrDefault();
    }

    /// <summary>Смена пароля после правильного ответа на секретный вопрос. Возвращает текст ошибки или null.</summary>
    public string? ResetPassword(string email, string answer, string newPassword, string repeat)
    {
        email = email.Trim().ToLowerInvariant();
        var user = context.Пользователь.FirstOrDefault(u => u.ЭлектроннаяПочта == email);
        if (user == null) return "Пользователь с таким e-mail не найден.";
        if (user.ОтветНаСекретныйВопрос != Hash(email, NormalizeAnswer(answer))) return "Неверный ответ на секретный вопрос.";
        if (!PasswordConditions(newPassword).All(c => c.Ok)) return "Новый пароль не соответствует условиям: не менее 6 символов, заглавная и строчная буквы, цифра.";
        if (newPassword != repeat) return "Пароли не совпадают.";
        user.Пароль = Hash(email, newPassword);
        context.SaveChanges();
        return null;
    }
}
