namespace CrmSales;

/// <summary>Демонстрационные данные: отдел продаж, клиенты (в том числе дубли), заявки, план внедрения.</summary>
public static class SeedData
{
    public static CrmData Create()
    {
        var rnd = new Random(7);
        DateTime Ago(int days, int hour = 10) => DateTime.Today.AddDays(-days).AddHours(hour).AddMinutes(rnd.Next(60));

        User U(string id, string login, string pass, string name, Role role, bool pilot) =>
            new() { Id = id, Login = login, PasswordHash = Db.Hash(login, pass), Name = name, Role = role, Pilot = pilot };

        var d = new CrmData();
        d.Users.AddRange(new[]
        {
            U("u_admin", "admin", "admin", "Соколов Игорь", Role.Admin, false),
            U("u_head", "head", "head", "Власова Марина", Role.Head, false),
            U("u_m1", "petrov", "1234", "Петров Алексей", Role.Manager, true),
            U("u_m2", "ivanova", "1234", "Иванова Ольга", Role.Manager, true),
            U("u_m3", "kuznetsov", "1234", "Кузнецов Денис", Role.Manager, true),
            U("u_m4", "smirnova", "1234", "Смирнова Анна", Role.Manager, true),
            U("u_m5", "orlov", "1234", "Орлов Павел", Role.Manager, true),
            U("u_m6", "fedorova", "1234", "Фёдорова Юлия", Role.Manager, false),
            U("u_m7", "morozov", "1234", "Морозов Артём", Role.Manager, false),
        });
        var mgr = d.Users.Where(u => u.Role == Role.Manager).Select(u => u.Id).ToArray();

        string[][] raw =
        {
            new[] { "ООО «СтройМастер»", "Гончаров Виктор", "+7 (495) 123-45-67", "info@stroymaster.ru", "7701234567", "Москва", "Сайт" },
            new[] { "АО «ТехноПарк»", "Лебедева Светлана", "+7 (812) 555-12-34", "zakaz@technopark.ru", "7802345678", "Санкт-Петербург", "Выставка" },
            new[] { "ИП Захаров Н. С.", "Захаров Николай", "8 912 345-67-89", "zaharov.ns@mail.ru", "662312345678", "Екатеринбург", "Звонок" },
            new[] { "ООО «Северный ветер»", "Андреева Ксения", "+7 (8182) 20-30-40", "office@sev-veter.ru", "2901234567", "Архангельск", "Рекомендация" },
            new[] { "ООО «АгроСнаб»", "Кириллов Олег", "+7 (863) 244-11-22", "agrosnab@yandex.ru", "6161234567", "Ростов-на-Дону", "Сайт" },
            new[] { "ООО «МедЛайн»", "Полякова Ирина", "+7 (843) 212-00-01", "buy@medline.ru", "1655123456", "Казань", "Выставка" },
            new[] { "ООО «Логистик Плюс»", "Тихонов Сергей", "+7 (383) 310-20-30", "t.sergey@logplus.ru", "5406123456", "Новосибирск", "Звонок" },
            new[] { "ООО «ВекторСофт»", "Егорова Дарья", "+7 (495) 640-10-10", "sales@vectorsoft.ru", "7714123456", "Москва", "Сайт" },
            new[] { "ООО «Уральский металл»", "Белов Константин", "+7 (343) 371-71-71", "snab@uralmet.ru", "6671123456", "Екатеринбург", "Рекомендация" },
            new[] { "ООО «Кондитер»", "Мельникова Татьяна", "+7 (4862) 55-66-77", "zakupki@konditer.ru", "5752123456", "Орёл", "Сайт" },
            new[] { "АО «ЭнергоСервис»", "Громов Андрей", "+7 (846) 270-80-90", "es@energoservis.ru", "6315123456", "Самара", "Тендер" },
            new[] { "ООО «Фармация»", "Данилова Елена", "+7 (831) 433-22-11", "info@pharmacia-nn.ru", "5260123456", "Нижний Новгород", "Звонок" },
            new[] { "ООО «Автоцентр Юг»", "Карпов Максим", "+7 (861) 200-30-00", "karpov@avtoug.ru", "2310123456", "Краснодар", "Выставка" },
            new[] { "ООО «Печатный двор»", "Зайцева Наталья", "+7 (4852) 30-40-50", "print@pdvor.ru", "7604123456", "Ярославль", "Сайт" },
            new[] { "ООО «ГрандОтель»", "Семёнов Роман", "+7 (4212) 41-00-00", "hotel@grand-hb.ru", "2721123456", "Хабаровск", "Рекомендация" },
            new[] { "ИП Васильева А. П.", "Васильева Алла", "+7 (910) 777-88-99", "vasileva.ap@gmail.com", "772512345678", "Москва", "Звонок" },
            new[] { "ООО «ДомСтрой»", "Никитин Глеб", "+7 (4232) 22-33-44", "domstroy@vl.ru", "2540123456", "Владивосток", "Сайт" },
            new[] { "ООО «Чистый город»", "Соловьёва Вера", "+7 (3452) 50-60-70", "clean@chgorod.ru", "7203123456", "Тюмень", "Тендер" },
            new[] { "ООО «ИнфоТрейд»", "Макаров Илья", "+7 (495) 980-00-11", "it@infotrade.ru", "7729123456", "Москва", "Сайт" },
            new[] { "ООО «Сибирский лес»", "Волков Тимур", "+7 (3912) 65-43-21", "les@sibles.ru", "2465123456", "Красноярск", "Выставка" },
            new[] { "ООО «Молочный край»", "Яковлева Мария", "+7 (3812) 30-30-30", "moloko@mkray.ru", "5503123456", "Омск", "Рекомендация" },
            new[] { "ООО «ТрансКарго»", "Алексеев Руслан", "+7 (4722) 20-20-20", "cargo@transcargo.ru", "3123123456", "Белгород", "Звонок" },
            new[] { "ООО «Оптика Люкс»", "Комарова Лилия", "+7 (8412) 56-78-90", "lux@optika58.ru", "5836123456", "Пенза", "Сайт" },
            new[] { "ООО «Альфа-Климат»", "Борисов Степан", "+7 (4732) 39-00-39", "klimat@alfa-k.ru", "3664123456", "Воронеж", "Тендер" },
            // Дубли, попавшие из разных источников (Excel и почта) — для демонстрации объединения
            new[] { "Строймастер", "Гончаров В.", "8 (495) 123-45-67", "", "", "Москва", "Почта" },
            new[] { "ООО Медлайн", "Полякова И.", "", "BUY@MEDLINE.RU", "", "Казань", "Excel" },
        };
        for (int i = 0; i < raw.Length; i++)
        {
            var r = raw[i];
            d.Clients.Add(new Client
            {
                Id = "c" + (i + 1), Name = r[0], Contact = r[1], Phone = r[2], Email = r[3], Inn = r[4], City = r[5], Source = r[6],
                OwnerId = mgr[i % mgr.Length], Created = Ago(80 - i * 2), Updated = Ago(Math.Max(0, 40 - i)),
            });
        }

        string[] titles = { "Поставка оборудования", "Годовой сервисный договор", "Пробная партия", "Расширение лицензии", "Комплексное обслуживание", "Доп. соглашение", "Поставка расходных материалов" };
        DealStatus[] statuses = { DealStatus.Won, DealStatus.Won, DealStatus.Lost, DealStatus.InWork, DealStatus.Offer, DealStatus.New, DealStatus.Won, DealStatus.InWork };
        for (int i = 0; i < 24; i++)
        {
            var c = d.Clients[i];
            for (int k = 0; k < 1 + i % 3; k++)
            {
                var st = statuses[(i + k) % statuses.Length];
                var created = Ago(5 + (i * 7 + k * 13) % 85, 9);
                var closedAfter = 6 + (i * 11 + k * 5) % 40;
                d.Deals.Add(new Deal
                {
                    ClientId = c.Id, OwnerId = c.OwnerId, Title = titles[(i + k) % titles.Length],
                    Amount = 40000 + (i * 37 + k * 53) % 30 * 15000, Status = st, Created = created,
                    Closed = st is DealStatus.Won or DealStatus.Lost ? created.AddHours(closedAfter) : null,
                });
            }
            d.Interactions.Add(new Interaction { ClientId = c.Id, UserId = c.OwnerId, Type = InteractionType.Call, Text = "Первичный звонок, выяснили потребность.", Date = Ago(60 - i) });
            d.Interactions.Add(new Interaction { ClientId = c.Id, UserId = c.OwnerId, Type = InteractionType.Mail, Text = "Отправлено коммерческое предложение и прайс-лист.", Date = Ago(40 - i) });
            if (i % 2 == 0)
                d.Interactions.Add(new Interaction { ClientId = c.Id, UserId = c.OwnerId, Type = InteractionType.Meeting, Text = "Встреча в офисе клиента, обсудили условия оплаты.", Date = Ago(20 - i % 15) });
        }

        (string, string, StageStatus)[] plan =
        {
            ("Анализ текущего учёта клиентов", "Какие данные, где хранятся, какие проблемы", StageStatus.Done),
            ("Формирование требований", "Функции, права доступа, отчёты", StageStatus.Done),
            ("Подготовка инфраструктуры", "Сервер или облако, учётные записи", StageStatus.Done),
            ("Установка и настройка CRM", "Справочники, роли, поля карточки", StageStatus.Done),
            ("Перенос клиентской базы", "Резервная копия исходных данных, импорт, сверка", StageStatus.InProgress),
            ("Тестирование", "Функциональное и пользовательское", StageStatus.NotStarted),
            ("Обучение пилотной группы", "5 менеджеров", StageStatus.NotStarted),
            ("Пилотная эксплуатация", "2–4 недели, сбор обратной связи", StageStatus.NotStarted),
            ("Полный запуск", "Устранение замечаний, обучение остальных, запуск на весь отдел", StageStatus.NotStarted),
        };
        for (int i = 0; i < plan.Length; i++)
            d.Plan.Add(new Stage { Id = "s" + (i + 1), Title = plan[i].Item1, Hint = plan[i].Item2, Status = plan[i].Item3 });

        (string, string, string, RiskStatus)[] risks =
        {
            ("Потеря или повреждение данных при переносе", "Средняя", "Резервная копия перед импортом, сверка количества записей", RiskStatus.Controlled),
            ("Сопротивление сотрудников новой системе", "Высокая", "Вовлечение менеджеров, демонстрация выгод, поддержка руководителя", RiskStatus.Open),
            ("Недостаточное обучение → ошибки пользователей", "Средняя", "Практические занятия, инструкции, консультант на первые недели", RiskStatus.Open),
            ("Несовместимость с почтой и телефонией", "Низкая", "Проверка интеграций на этапе тестирования", RiskStatus.Controlled),
            ("Срыв сроков из-за нехватки ресурсов", "Средняя", "Резерв времени в плане, еженедельный контроль", RiskStatus.Open),
        };
        foreach (var r in risks)
            d.Risks.Add(new Risk { Title = r.Item1, Probability = r.Item2, Measure = r.Item3, Status = r.Item4 });

        d.Feedback.Add(new Feedback { UserId = "u_m1", Date = Ago(3), Kind = FeedbackKind.Idea, Rating = 4, Text = "Удобно видеть всю историю по клиенту. Хотелось бы напоминания о звонках." });
        d.Feedback.Add(new Feedback { UserId = "u_m2", Date = Ago(2), Kind = FeedbackKind.Problem, Rating = 3, Text = "При импорте из Excel не распознался столбец «Тел. моб.».", Resolved = true });
        d.Feedback.Add(new Feedback { UserId = "u_m4", Date = Ago(1), Kind = FeedbackKind.Praise, Rating = 5, Text = "Поиск клиента занимает секунды, раньше искала в почте по 2–3 минуты." });

        foreach (var s in new[] { 14, 9, 22, 11, 7, 18, 12, 8, 15, 10, 6, 13 })
            d.Searches.Add(new SearchMeasure { Seconds = s, Date = Ago(s % 10) });

        d.Log.Add(new LogEntry { User = "Система", Action = "Созданы демонстрационные данные" });
        return d;
    }
}
