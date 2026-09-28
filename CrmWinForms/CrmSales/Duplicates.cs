using System.Text.RegularExpressions;

namespace CrmSales;

/// <summary>Поиск дублей клиентов по телефону, email, ИНН и названию.</summary>
public static class Duplicates
{
    public static string NormPhone(string? p)
    {
        var d = Regex.Replace(p ?? "", @"\D", "");
        if (d.Length == 11 && (d[0] == '8' || d[0] == '7')) d = d[1..];
        return d.Length >= 10 ? d[^10..] : "";
    }

    public static string NormEmail(string? e) => (e ?? "").Trim().ToLowerInvariant();

    public static string NormInn(string? i) => Regex.Replace(i ?? "", @"\D", "");

    public static string NormName(string? n)
    {
        var s = " " + Regex.Replace((n ?? "").ToLowerInvariant().Replace('ё', 'е'), "[«»\"'.,()\\-]", " ") + " ";
        s = Regex.Replace(s, @"\s(ооо|оао|зао|пао|ао|ип)(?=\s)", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    public static List<string> Keys(string? name, string? phone, string? email, string? inn)
    {
        var k = new List<string>();
        if (NormPhone(phone) is { Length: > 0 } p) k.Add("p:" + p);
        if (NormEmail(email) is { Length: > 0 } e) k.Add("e:" + e);
        if (NormInn(inn) is { Length: >= 10 } i) k.Add("i:" + i);
        if (NormName(name) is { Length: > 2 } n) k.Add("n:" + n);
        return k;
    }

    public static List<string> Keys(Client c) => Keys(c.Name, c.Phone, c.Email, c.Inn);

    public static Client? Find(Client probe, IEnumerable<Client> list)
    {
        var keys = Keys(probe).ToHashSet();
        return list.FirstOrDefault(x => x.Id != probe.Id && Keys(x).Any(keys.Contains));
    }

    /// <summary>Группы клиентов, связанных общими ключами (первый в группе — самый старый, он основной).</summary>
    public static List<List<Client>> Groups(IEnumerable<Client> source)
    {
        var list = source.ToList();
        var parent = list.ToDictionary(c => c.Id, c => c.Id);
        string Find(string x) => parent[x] == x ? x : parent[x] = Find(parent[x]);

        var byKey = new Dictionary<string, string>();
        foreach (var c in list)
            foreach (var k in Keys(c))
                if (byKey.TryGetValue(k, out var other)) parent[Find(c.Id)] = Find(other);
                else byKey[k] = c.Id;

        return list.GroupBy(c => Find(c.Id))
                   .Where(g => g.Count() > 1)
                   .Select(g => g.OrderBy(c => c.Created).ToList())
                   .ToList();
    }

    /// <summary>Объединяет дубли в основную запись: заполняет пустые поля, переносит заявки и историю.</summary>
    public static void Merge(List<Client> group)
    {
        var main = group[0];
        foreach (var d in group.Skip(1))
        {
            if (main.Contact == "") main.Contact = d.Contact;
            if (main.Phone == "") main.Phone = d.Phone;
            if (main.Email == "") main.Email = d.Email;
            if (main.Inn == "") main.Inn = d.Inn;
            if (main.City == "") main.City = d.City;
            foreach (var deal in Db.Data.Deals.Where(x => x.ClientId == d.Id)) deal.ClientId = main.Id;
            foreach (var i in Db.Data.Interactions.Where(x => x.ClientId == d.Id)) i.ClientId = main.Id;
            Db.AddHistory(main.Id, InteractionType.System, $"Объединено с дублем «{d.Name}»");
            Db.Data.Clients.Remove(d);
        }
        main.Updated = DateTime.Now;
        Db.Log($"Объединены дубли в «{main.Name}» ({group.Count - 1})");
    }
}
