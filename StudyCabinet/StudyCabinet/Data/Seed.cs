using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyCabinet.Models;
using StudyCabinet.Services;

namespace StudyCabinet.Data;

/// <summary>
/// Начальные данные: преподаватель, студенты двух групп, две дисциплины с темами, лекциями,
/// контрольными вопросами и тестами, а также прогресс студентов для демонстрации отчётов.
/// Выполняется только для пустой базы.
/// </summary>
public static class Seed
{
    // ---------- Конструкторы для краткой записи ----------
    static AnswerOption O(string text, bool correct = false) => new() { Text = text, IsCorrect = correct };
    static AnswerOption M(string left, string right) => new() { Text = left, MatchText = right, IsCorrect = true };
    static List<AnswerOption> Opts(params AnswerOption[] o) { for (int i = 0; i < o.Length; i++) o[i].Order = i; return o.ToList(); }

    static ControlQuestion CQ(QuestionType t, string text, params AnswerOption[] o) => new() { Type = t, Text = text, Options = Opts(o) };
    static TestQuestion TQ(QuestionType t, string text, params AnswerOption[] o) => new() { Type = t, Text = text, Options = Opts(o) };
    /// <summary>Краткий ответ или пропущенное слово: все варианты — допустимые правильные ответы.</summary>
    static AnswerOption[] Accept(params string[] answers) => answers.Select(a => O(a, true)).ToArray();

    static Lecture L(int number, string title, string description, string content, ControlQuestion[] control, Test? test,
        bool requireControlCorrect = false)
    {
        for (int i = 0; i < control.Length; i++) control[i].Order = i;
        if (test != null) for (int i = 0; i < test.Questions.Count; i++) test.Questions[i].Order = i;
        return new Lecture
        {
            Number = number, Title = title, Description = description, Content = content.Trim(), Order = number,
            ControlQuestions = control.ToList(), Test = test, RequireControlCorrect = requireControlCorrect,
        };
    }

    static Test T(int pass, int attempts, int minutes, params TestQuestion[] q) =>
        new() { PassPercent = pass, MaxAttempts = attempts, TimeLimitMinutes = minutes, Questions = q.ToList() };

    public static async Task RunAsync(AppDb db, IPasswordHasher<User> hasher, MaterialStore materials, string contentRoot)
    {
        if (await db.Users.AnyAsync()) return;

        // ---------- Группы и пользователи ----------
        var g21 = new StudentGroup { Name = "ИС-21" };
        var g22 = new StudentGroup { Name = "ИС-22" };
        db.StudentGroups.AddRange(g21, g22);

        User U(string login, string pass, string name, string role, StudentGroup? group, int daysAgo)
        {
            var u = new User { Login = login, FullName = name, Role = role, Email = login + "@college.local", CreatedAt = DateTime.Now.AddDays(-daysAgo) };
            u.PasswordHash = hasher.HashPassword(u, pass);
            if (group != null) u.Memberships.Add(new GroupMembership { Group = group });
            return u;
        }
        var teacher = U("teacher", "Teacher123", "Смирнова Елена Викторовна", Roles.Teacher, null, 40);
        var kirchu = U("student", "Student123", "Кирчу Игорь", Roles.Student, g21, 30);
        var petrova = U("petrova", "Student123", "Петрова Анна", Roles.Student, g21, 30);
        var sidorov = U("sidorov", "Student123", "Сидоров Павел", Roles.Student, g21, 29);
        var kuznetsova = U("kuznetsova", "Student123", "Кузнецова Мария", Roles.Student, g22, 28);
        var orlov = U("orlov", "Student123", "Орлов Дмитрий", Roles.Student, g22, 5);
        db.Users.AddRange(teacher, kirchu, petrova, sidorov, kuznetsova, orlov);

        // ---------- Дисциплина 1 ----------
        var d1 = new Discipline
        {
            Title = "Внедрение и поддержка компьютерных систем",
            Description = "Тестирование программного обеспечения, внедрение программ в организации, сопровождение и резервное копирование.",
            CreatedAt = DateTime.Now.AddDays(-35),
        };
        var t1 = new Topic { Title = "Тестирование программного обеспечения", Order = 1 };
        var t2 = new Topic { Title = "Внедрение программного обеспечения", Order = 2 };
        d1.Topics.AddRange(new[] { t1, t2 });

        t1.Lectures.Add(L(1, "Основы тестирования программного обеспечения", "Что такое тестирование, зачем оно нужно и на каких принципах строится.", @"
# Что такое тестирование
**Тестирование программного обеспечения** — это проверка соответствия программы требованиям, которая выполняется путём наблюдения за её работой в специально подготовленных ситуациях. Цель тестирования — найти дефекты до того, как их найдут пользователи, и дать уверенность в качестве продукта.

## Верификация и валидация
- **Верификация** отвечает на вопрос «правильно ли мы делаем продукт?» — соответствует ли программа техническому заданию.
- **Валидация** отвечает на вопрос «тот ли продукт мы делаем?» — решает ли программа реальные задачи пользователя.

## Семь принципов тестирования
- Тестирование показывает наличие дефектов, но не доказывает их отсутствие.
- Исчерпывающее тестирование невозможно: проверить все комбинации входных данных нельзя.
- Раннее тестирование экономит время и деньги: ошибку в требованиях исправить дешевле, чем в готовой программе.
- Дефекты скапливаются в небольшом числе модулей.
- Парадокс пестицида: одни и те же тесты со временем перестают находить новые ошибки.
- Тестирование зависит от контекста: сайт и программа для банка проверяются по-разному.
- Отсутствие ошибок не гарантирует полезность программы.

## Основные термины
**Дефект (баг)** — несоответствие фактического результата ожидаемому. **Тест-кейс** — описание одной проверки. **Тестовое покрытие** — доля требований или кода, проверенная тестами.",
            new[]
            {
                CQ(QuestionType.ShortText, "Как называется проверка соответствия программы требованиям путём наблюдения за её работой?", Accept("тестирование", "тестирование по", "тестирование программного обеспечения")),
                CQ(QuestionType.Single, "На какой вопрос отвечает верификация?",
                    O("Правильно ли мы делаем продукт?", true), O("Тот ли продукт мы делаем?"), O("Сколько стоит продукт?")),
                CQ(QuestionType.Essay, "Объясните своими словами, почему исчерпывающее тестирование невозможно. Приведите пример."),
            },
            T(70, 3, 10,
                TQ(QuestionType.Single, "Что показывает тестирование?",
                    O("Наличие дефектов", true), O("Отсутствие дефектов"), O("Скорость разработки"), O("Стоимость проекта")),
                TQ(QuestionType.Multiple, "Какие утверждения относятся к принципам тестирования?",
                    O("Исчерпывающее тестирование невозможно", true), O("Дефекты скапливаются в небольшом числе модулей", true),
                    O("Тестирование доказывает отсутствие ошибок"), O("Раннее тестирование экономит время и деньги", true)),
                TQ(QuestionType.ShortText, "Как называется несоответствие фактического результата ожидаемому? (одно слово)", Accept("дефект", "баг", "ошибка")),
                TQ(QuestionType.Gap, "Заполните пропуск: «Парадокс ______: одни и те же тесты перестают находить новые ошибки».", Accept("пестицида")),
                TQ(QuestionType.Matching, "Сопоставьте понятие и вопрос, на который оно отвечает.",
                    M("Верификация", "Правильно ли мы делаем продукт?"), M("Валидация", "Тот ли продукт мы делаем?"), M("Тестовое покрытие", "Какая доля требований проверена?")),
                TQ(QuestionType.Single, "Когда дешевле всего исправить ошибку?",
                    O("На этапе требований", true), O("После выпуска программы"), O("Во время сопровождения")))));

        t1.Lectures.Add(L(2, "Виды и уровни тестирования", "Модульное, интеграционное, системное и приёмочное тестирование; функциональные и нефункциональные проверки.", @"
# Уровни тестирования
- **Модульное (unit)** — проверка отдельных функций и классов, обычно пишется программистами.
- **Интеграционное** — проверка взаимодействия модулей между собой, например программы и базы данных.
- **Системное** — проверка всей системы целиком на соответствие требованиям.
- **Приёмочное** — проверка заказчиком или пользователями перед вводом в эксплуатацию.

# Виды тестирования
## Функциональное
Проверяет, **что** делает программа: правильно ли выполняются операции, расчёты, сохранение данных.

## Нефункциональное
Проверяет, **как** работает программа:
- нагрузочное — работа при большом числе пользователей;
- тестирование удобства (юзабилити);
- тестирование безопасности — защита данных и разграничение прав;
- тестирование совместимости — разные браузеры и операционные системы.

## Чёрный и белый ящик
При тестировании **чёрного ящика** тестировщик не видит код и проверяет только входы и выходы. При тестировании **белого ящика** проверки строятся на знании внутреннего устройства программы.

## Регрессионное тестирование
После каждого исправления нужно убедиться, что ничего не сломалось в уже работавших частях программы — это **регрессионное тестирование**.",
            new[]
            {
                CQ(QuestionType.Matching, "Сопоставьте уровень тестирования и что на нём проверяется.",
                    M("Модульное", "Отдельные функции и классы"), M("Интеграционное", "Взаимодействие модулей"), M("Приёмочное", "Готовность к сдаче заказчику")),
                CQ(QuestionType.Gap, "Заполните пропуск: «При тестировании ______ ящика тестировщик не видит программный код».", Accept("черного", "чёрного")),
                CQ(QuestionType.Single, "Какой вид тестирования проверяет работу при большом числе пользователей?",
                    O("Нагрузочное", true), O("Модульное"), O("Юзабилити")),
            },
            T(70, 3, 0,
                TQ(QuestionType.Single, "Кто обычно пишет модульные тесты?", O("Программисты", true), O("Заказчик"), O("Пользователи")),
                TQ(QuestionType.Multiple, "Что относится к нефункциональному тестированию?",
                    O("Нагрузочное", true), O("Тестирование безопасности", true), O("Проверка правильности расчёта суммы"), O("Тестирование совместимости", true)),
                TQ(QuestionType.ShortText, "Как называется проверка того, что исправление не сломало работавшие части программы?", Accept("регрессионное", "регрессионное тестирование", "регресс")),
                TQ(QuestionType.Matching, "Сопоставьте уровень тестирования и его исполнителя.",
                    M("Модульное", "Программист"), M("Системное", "Тестировщик"), M("Приёмочное", "Заказчик")),
                TQ(QuestionType.Single, "Тестирование на основе знания программного кода — это…", O("Белый ящик", true), O("Чёрный ящик"), O("Приёмочное")))));

        var l3 = L(3, "Тест-кейсы и отчёты о дефектах", "Как описать проверку и оформить найденную ошибку. Подробный материал — в файле PDF.", @"
Основной материал лекции — в прикреплённом файле PDF: структура тест-кейса, поля отчёта о дефекте и его жизненный цикл.

# Кратко
- **Тест-кейс** описывает одну проверку: предусловия, шаги и ожидаемый результат.
- **Отчёт о дефекте** нужен, чтобы разработчик мог воспроизвести и исправить ошибку.
- **Серьёзность** показывает, насколько дефект мешает работе, **приоритет** — как срочно его исправлять.
- Повторная проверка исправленной ошибки — **ретест**.",
            new[]
            {
                CQ(QuestionType.Multiple, "Какие поля обязательно есть в отчёте о дефекте?",
                    O("Шаги воспроизведения", true), O("Ожидаемый и фактический результат", true), O("Любимый цвет тестировщика"), O("Серьёзность", true)),
                CQ(QuestionType.ShortText, "Как называется документ, описывающий одну проверку: предусловия, шаги и ожидаемый результат?", Accept("тест-кейс", "тест кейс", "тесткейс", "test case")),
            },
            T(60, 0, 0,
                TQ(QuestionType.Single, "Что показывает приоритет дефекта?", O("Как срочно его исправлять", true), O("Насколько он мешает работе"), O("Кто его нашёл")),
                TQ(QuestionType.Gap, "Повторная проверка исправленного дефекта называется ______.", Accept("ретест", "ретестом", "retest")),
                TQ(QuestionType.Matching, "Сопоставьте поле тест-кейса и его содержание.",
                    M("Предусловия", "Что сделать до проверки"), M("Шаги", "Действия пользователя"), M("Ожидаемый результат", "Правильное поведение программы")),
                TQ(QuestionType.Single, "Сколько дефектов описывают в одном отчёте?", O("Один", true), O("Все найденные за день"), O("Не больше пяти")),
                TQ(QuestionType.Multiple, "Что полезно приложить к отчёту о дефекте?", O("Скриншот", true), O("Журнал ошибок", true), O("Описание окружения", true), O("Личное мнение о программисте"))));
        t1.Lectures.Add(l3);

        var l4 = L(4, "Этапы внедрения программного обеспечения", "Подготовка, установка, обучение, опытная и промышленная эксплуатация. Материал — документ Word.", @"
Основной материал лекции — в прикреплённом документе Word. Его можно читать прямо здесь, на вкладке «Документ», или скачать.

# Главное
- Внедрение — это не только установка, но и настройка, перенос данных и обучение людей.
- После установки идёт **опытная эксплуатация**: работа в реальных условиях со сбором замечаний.
- Стратегии перехода: параллельная, поэтапная, пилотная и «большой взрыв».",
            new[]
            {
                CQ(QuestionType.Matching, "Сопоставьте стратегию перехода на новую систему и её описание.",
                    M("Параллельная", "Старая и новая системы работают одновременно"), M("Пилотная", "Сначала в одном подразделении"), M("Большой взрыв", "Одномоментный переход")),
                CQ(QuestionType.Essay, "Опишите, какие действия нужно выполнить на компьютере перед установкой веб-приложения с базой данных."),
            },
            T(70, 2, 15,
                TQ(QuestionType.Single, "Как называется работа программы в реальных условиях со сбором замечаний?", O("Опытная эксплуатация", true), O("Модульное тестирование"), O("Проектирование")),
                TQ(QuestionType.Multiple, "Что входит в подготовку рабочего места?",
                    O("Установка среды выполнения и СУБД", true), O("Создание базы данных", true), O("Покупка нового монитора"), O("Создание учётных записей", true)),
                TQ(QuestionType.ShortText, "Какая стратегия перехода самая рискованная? (два слова)", Accept("большой взрыв")),
                TQ(QuestionType.Single, "Какой документ подтверждает ввод системы в эксплуатацию?", O("Акт ввода в эксплуатацию", true), O("Техническое задание"), O("Баг-репорт")),
                TQ(QuestionType.Gap, "Внедрение — это установка, настройка и ______ пользователей.", Accept("обучение"))));
        t2.Lectures.Add(l4);

        t2.Lectures.Add(L(5, "Сопровождение и резервное копирование", "Что входит в сопровождение программы, правило 3-2-1, проверка резервных копий.", @"
# Сопровождение
Сопровождение начинается после внедрения и длится всё время работы программы. В него входят:
- контроль доступности программы и подключения к базе данных;
- резервное копирование и восстановление после сбоев;
- установка исправлений и обновлений, проверка совместимости после обновления;
- анализ журналов ошибок и устранение дефектов;
- контроль прав доступа и актуализация инструкций.

# Резервное копирование
## Правило 3-2-1
- **3** копии данных,
- на **2** разных носителях,
- **1** копия хранится в другом месте (не в том же кабинете).

## Проверка копий
Резервная копия, которую ни разу не пробовали восстановить, — не копия. Регулярно выполняйте пробное восстановление на тестовом компьютере.

## Виды копий
**Полная** копия содержит все данные. **Инкрементная** — только изменения с момента последней копии любого вида. **Дифференциальная** — изменения с момента последней полной копии.",
            new[]
            {
                CQ(QuestionType.Gap, "В правиле 3-2-1 одна копия хранится в ______ месте.", Accept("другом", "другом месте", "удаленном", "удалённом")),
                CQ(QuestionType.Single, "Какая копия содержит изменения с момента последней полной копии?",
                    O("Дифференциальная", true), O("Инкрементная"), O("Полная")),
            },
            T(70, 3, 0,
                TQ(QuestionType.Multiple, "Что входит в сопровождение программы?",
                    O("Резервное копирование", true), O("Анализ журналов ошибок", true), O("Установка обновлений", true), O("Выбор цвета логотипа")),
                TQ(QuestionType.ShortText, "Сколько копий данных требует правило 3-2-1? (цифрой)", Accept("3", "три")),
                TQ(QuestionType.Single, "Как убедиться, что резервная копия рабочая?", O("Выполнить пробное восстановление", true), O("Посмотреть размер файла"), O("Никак")),
                TQ(QuestionType.Matching, "Сопоставьте вид копии и её содержимое.",
                    M("Полная", "Все данные"), M("Инкрементная", "Изменения с последней любой копии"), M("Дифференциальная", "Изменения с последней полной копии")))));

        // ---------- Дисциплина 2 ----------
        var d2 = new Discipline
        {
            Title = "Основы алгоритмизации и программирования",
            Description = "Понятие алгоритма, способы записи, основные алгоритмические конструкции.",
            CreatedAt = DateTime.Now.AddDays(-20),
        };
        var t3 = new Topic { Title = "Алгоритмы и их свойства", Order = 1 };
        d2.Topics.Add(t3);
        t3.Lectures.Add(L(1, "Понятие алгоритма", "Определение алгоритма, свойства и способы записи.", @"
# Алгоритм
**Алгоритм** — это точное описание последовательности действий, которая приводит к решению задачи за конечное число шагов.

## Свойства алгоритма
- **Дискретность** — алгоритм состоит из отдельных шагов.
- **Понятность** — каждый шаг понятен исполнителю.
- **Определённость** — каждый шаг однозначен.
- **Результативность** — алгоритм приводит к результату.
- **Конечность** — результат получается за конечное число шагов.
- **Массовость** — алгоритм решает целый класс однотипных задач.

## Способы записи
Словесный, блок-схема, псевдокод, программа на языке программирования.",
            new[]
            {
                CQ(QuestionType.ShortText, "Как называется свойство алгоритма «состоит из отдельных шагов»?", Accept("дискретность")),
                CQ(QuestionType.Single, "Что из перечисленного — способ записи алгоритма?", O("Блок-схема", true), O("Таблица умножения"), O("Операционная система")),
            },
            T(70, 3, 0,
                TQ(QuestionType.Multiple, "Какие свойства есть у алгоритма?", O("Конечность", true), O("Массовость", true), O("Случайность"), O("Определённость", true)),
                TQ(QuestionType.Matching, "Сопоставьте свойство и его смысл.",
                    M("Результативность", "Приводит к результату"), M("Понятность", "Каждый шаг понятен исполнителю"), M("Массовость", "Решает класс задач")),
                TQ(QuestionType.Gap, "Графический способ записи алгоритма — блок-______.", Accept("схема")),
                TQ(QuestionType.Single, "Алгоритм должен завершаться за…", O("Конечное число шагов", true), O("Одну секунду"), O("Бесконечное число шагов")))));
        t3.Lectures.Add(L(2, "Ветвления и циклы", "Условный оператор, циклы с предусловием, постусловием и счётчиком.", @"
# Ветвление
**Ветвление** — выбор действия в зависимости от условия: «если условие верно — сделать одно, иначе — другое».

# Циклы
- **Цикл с предусловием** (while) проверяет условие перед каждым повтором и может не выполниться ни разу.
- **Цикл с постусловием** (do…while) выполняется хотя бы один раз.
- **Цикл со счётчиком** (for) повторяется заданное число раз.

Каждый повтор тела цикла называется **итерацией**.",
            new[]
            {
                CQ(QuestionType.Single, "Какой цикл выполняется хотя бы один раз?", O("С постусловием", true), O("С предусловием"), O("Любой")),
                CQ(QuestionType.Gap, "Один повтор тела цикла называется ______.", Accept("итерацией", "итерация")),
            },
            T(70, 3, 0,
                TQ(QuestionType.Matching, "Сопоставьте цикл и ключевое слово в C#.", M("С предусловием", "while"), M("С постусловием", "do"), M("Со счётчиком", "for")),
                TQ(QuestionType.Single, "Может ли цикл с предусловием не выполниться ни разу?", O("Да", true), O("Нет")),
                TQ(QuestionType.ShortText, "Как называется выбор действия в зависимости от условия?", Accept("ветвление")))));

        db.Disciplines.AddRange(d1, d2);
        db.AuditLog.Add(new AuditEntry { At = DateTime.Now.AddDays(-35), UserName = "Система", Action = "Начальные данные", Details = "Созданы учётные записи, группы, дисциплины, лекции и тесты" });
        await db.SaveChangesAsync();

        // ---------- Файлы лекций ----------
        var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedMaterials");
        if (!Directory.Exists(seedDir)) seedDir = Path.Combine(contentRoot, "SeedMaterials");
        void Attach(Lecture l, string file) { var f = Path.Combine(seedDir, file); if (File.Exists(f)) materials.Import(l, f); }
        Attach(l3, "Тест-кейсы и отчёты о дефектах.pdf");
        Attach(l4, "Этапы внедрения ПО.docx");
        await db.SaveChangesAsync();

        // ---------- Прогресс студентов ----------
        var d1Lectures = t1.Lectures.Concat(t2.Lectures).OrderBy(l => l.Number).ToList();
        var d2Lectures = t3.Lectures.OrderBy(l => l.Number).ToList();

        // Кирчу Игорь: лекция 1 пройдена, по лекции 2 одна неудачная попытка
        await PassAsync(db, kirchu, d1Lectures[0], 18, 83);
        await AnswerControlAsync(db, kirchu, d1Lectures[1], 15);
        await AttemptAsync(db, kirchu, d1Lectures[1], 14, 40);
        // Петрова Анна: пройдены 1–3, по лекции 4 тест сдан, развёрнутый ответ ждёт проверки
        for (int i = 0; i < 3; i++) await PassAsync(db, petrova, d1Lectures[i], 20 - i * 4, 100 - i * 10);
        await AnswerControlAsync(db, petrova, d1Lectures[3], 2, essayPending: true);
        await AttemptAsync(db, petrova, d1Lectures[3], 2, 80);
        // Сидоров Павел: две неудачные попытки по лекции 1
        await AnswerControlAsync(db, sidorov, d1Lectures[0], 10);
        await AttemptAsync(db, sidorov, d1Lectures[0], 9, 33);
        await AttemptAsync(db, sidorov, d1Lectures[0], 7, 50);
        // Кузнецова Мария: дисциплина 1 пройдена полностью, по дисциплине 2 — первая лекция
        for (int i = 0; i < d1Lectures.Count; i++) await PassAsync(db, kuznetsova, d1Lectures[i], 25 - i * 4, 100 - (i % 2) * 20);
        await PassAsync(db, kuznetsova, d2Lectures[0], 3, 75);
        // Орлов Дмитрий только зарегистрировался

        db.AuditLog.Add(new AuditEntry { At = DateTime.Now.AddDays(-12), UserId = teacher.Id, UserName = teacher.FullName, Action = "Проверка ответа", Details = "Кирчу Игорь, лекция 1: развёрнутый ответ принят" });
        await db.SaveChangesAsync();
    }

    // ---------- Демонстрационный прогресс ----------

    /// <summary>Правильный ответ на вопрос (для демонстрационных данных).</summary>
    static string[] RightAnswer(IQuestion q) => q.Type switch
    {
        QuestionType.Single => new[] { q.Options.First(o => o.IsCorrect).Id.ToString() },
        QuestionType.Multiple => q.Options.Where(o => o.IsCorrect).Select(o => o.Id.ToString()).ToArray(),
        QuestionType.Matching => q.Options.Select(o => $"{o.Id}|{o.MatchText}").ToArray(),
        QuestionType.Essay => new[] { "Проверить все сочетания входных данных невозможно: даже у поля для числа от 1 до 1000 и второго такого же поля миллион комбинаций. Поэтому тестировщик выбирает самые важные и типичные случаи и граничные значения." },
        _ => new[] { q.Options.First().Text },
    };

    /// <summary>Неправильный ответ на вопрос.</summary>
    static string[] WrongAnswer(IQuestion q) => q.Type switch
    {
        QuestionType.Single or QuestionType.Multiple => new[] { q.Options.First(o => !o.IsCorrect).Id.ToString() },
        QuestionType.Matching => q.Options.Select((o, i) => $"{o.Id}|{q.Options[(i + 1) % q.Options.Count].MatchText}").ToArray(),
        _ => new[] { "не знаю" },
    };

    static async Task AnswerControlAsync(AppDb db, User s, Lecture l, int daysAgo, bool essayPending = false)
    {
        var at = DateTime.Now.AddDays(-daysAgo).AddHours(-2);
        foreach (var q in l.ControlQuestions)
        {
            var answer = RightAnswer(q);
            var ok = Grading.Check(q, answer);
            db.StudentAnswers.Add(new StudentAnswer
            {
                Student = s, ControlQuestionId = q.Id, Answer = Grading.ToJson(answer), AnsweredAt = at,
                Status = ok == null ? (essayPending ? AnswerStatus.Pending : AnswerStatus.Accepted) : ok.Value ? AnswerStatus.Correct : AnswerStatus.Wrong,
                CheckedAt = ok == null && !essayPending ? at.AddHours(20) : null,
                TeacherComment = ok == null && !essayPending ? "Хороший ответ, пример уместный." : null,
            });
        }
        db.LectureProgress.Add(new LectureProgress { Student = s, LectureId = l.Id, FirstOpenedAt = at.AddMinutes(-40) });
        await db.SaveChangesAsync();
    }

    /// <summary>Попытка теста с примерно заданным процентом правильных ответов.</summary>
    static async Task AttemptAsync(AppDb db, User s, Lecture l, int daysAgo, int targetPercent)
    {
        var test = l.Test!;
        var qs = test.Questions.OrderBy(q => q.Order).ToList();
        int needRight = (int)Math.Round(qs.Count * targetPercent / 100.0);
        var answers = new Dictionary<string, string[]>();
        int correct = 0;
        for (int i = 0; i < qs.Count; i++)
        {
            var a = i < needRight ? RightAnswer(qs[i]) : WrongAnswer(qs[i]);
            answers[qs[i].Id.ToString()] = a;
            if (Grading.Check(qs[i], a) == true) correct++;
        }
        int percent = correct * 100 / qs.Count;
        var start = DateTime.Now.AddDays(-daysAgo).AddMinutes(-30);
        db.TestAttempts.Add(new TestAttempt
        {
            Student = s, TestId = test.Id, StartedAt = start, FinishedAt = start.AddMinutes(4 + qs.Count),
            QuestionsJson = JsonSerializer.Serialize(qs.Select(q => new AttemptQuestion(q.Id, q.Options.OrderBy(o => o.Order).Select(o => o.Id).ToArray()))),
            AnswersJson = JsonSerializer.Serialize(answers),
            Correct = correct, Total = qs.Count, Percent = percent,
            Status = percent >= test.PassPercent ? AttemptStatus.Passed : AttemptStatus.Failed,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Лекция пройдена полностью: контрольные вопросы, тест и отметка о прохождении.</summary>
    static async Task PassAsync(AppDb db, User s, Lecture l, int daysAgo, int percent)
    {
        await AnswerControlAsync(db, s, l, daysAgo);
        if (l.Test != null) await AttemptAsync(db, s, l, daysAgo, Math.Max(percent, l.Test.PassPercent));
        var p = await db.LectureProgress.FirstAsync(x => x.StudentId == s.Id && x.LectureId == l.Id);
        p.Status = ProgressStatus.Completed;
        p.CompletedAt = DateTime.Now.AddDays(-daysAgo);
        await db.SaveChangesAsync();
    }
}

/// <summary>Вопрос, выданный в попытке теста, и порядок его вариантов.</summary>
public record AttemptQuestion(int Q, int[] O);
