namespace CrmSales;

public enum CritState { Ok, Warn, Bad }

public record Criterion(string Title, string Detail, CritState State);

/// <summary>Показатели для отчётов и критериев успешности внедрения (раздел 10 задания).</summary>
public static class Metrics
{
    public static double AvgDealHours(IEnumerable<Deal> deals)
    {
        var closed = deals.Where(d => d.Closed != null).ToList();
        return closed.Count == 0 ? 0 : closed.Average(d => d.Hours);
    }

    public static double AvgSearchSec()
    {
        var s = Db.Data.Searches.TakeLast(50).ToList();
        return s.Count == 0 ? 0 : s.Average(x => x.Seconds);
    }

    public static double? MigratedPercent()
    {
        var last = Db.Data.Imports.FirstOrDefault();
        if (last == null) return null;
        var withData = Math.Max(1, last.Total - last.Empty);
        return (last.Created + last.Merged + last.Updated + last.Unchanged) * 100.0 / withData;
    }

    public static (int Total, int Active) PilotActivity()
    {
        var since = DateTime.Now.AddDays(-30);
        var pilot = Db.Managers.Where(u => u.Pilot).ToList();
        int active = pilot.Count(u =>
            Db.Data.Interactions.Any(i => i.UserId == u.Id && i.Type != InteractionType.System && i.Date > since) ||
            Db.Data.Log.Any(l => l.User == u.Name && l.Date > since));
        return (pilot.Count, active);
    }

    public static double Gain(double now, double before) => now <= 0 || before <= 0 ? 0 : (1 - now / before) * 100;

    public static void RecordSearch(double seconds)
    {
        if (seconds <= 0 || seconds > 600) return;
        Db.Data.Searches.Add(new SearchMeasure { Seconds = Math.Round(seconds, 1) });
        if (Db.Data.Searches.Count > 500) Db.Data.Searches.RemoveAt(0);
    }

    public static List<Criterion> Criteria()
    {
        var st = Db.Data.Settings;
        var list = new List<Criterion>();

        var mig = MigratedPercent();
        list.Add(new("100 % клиентской базы перенесено без потерь",
            mig == null ? "Перенос из Excel ещё не выполнялся" : $"Последний перенос: {mig:0} % строк перенесено",
            mig == null ? CritState.Warn : mig >= 100 ? CritState.Ok : mig >= 95 ? CritState.Warn : CritState.Bad));

        var (total, active) = PilotActivity();
        list.Add(new("Все менеджеры пилотной группы работают в CRM",
            $"{active} из {total} активны за последние 30 дней",
            active == total ? CritState.Ok : active >= total * 0.7 ? CritState.Warn : CritState.Bad));

        var search = AvgSearchSec();
        var sg = Gain(search, st.BaselineSearchSec);
        list.Add(new("Время поиска информации о клиенте сократилось на 50 % и более",
            $"Сейчас {search:0.0} с против {st.BaselineSearchSec} с до внедрения (−{sg:0} %)",
            sg >= 50 ? CritState.Ok : sg >= 30 ? CritState.Warn : CritState.Bad));

        var deal = AvgDealHours(Db.Data.Deals);
        var dg = Gain(deal, st.BaselineDealHours);
        list.Add(new("Время обработки заявки сократилось на 30 % и более",
            $"Сейчас {deal:0.0} ч против {st.BaselineDealHours} ч до внедрения (−{dg:0} %)",
            dg >= 30 ? CritState.Ok : dg >= 15 ? CritState.Warn : CritState.Bad));

        var dups = Duplicates.Groups(Db.Data.Clients).Count;
        list.Add(new("Снизилось дублирование данных",
            dups == 0 ? "Дублей в базе нет" : $"Осталось групп дублей: {dups}",
            dups == 0 ? CritState.Ok : dups <= 3 ? CritState.Warn : CritState.Bad));

        list.Add(new("Руководитель получает отчёты из системы",
            "Вкладка «Отчёты» формируется автоматически по актуальным данным", CritState.Ok));
        return list;
    }

    public static string StateText(CritState s) => s switch
    {
        CritState.Ok => "Выполнено",
        CritState.Warn => "Близко",
        _ => "Не выполнено",
    };
}
