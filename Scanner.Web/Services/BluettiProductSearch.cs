using Scanner.WPF.Services;

namespace Scanner.Web.Services;

internal sealed class BluettiProductSearch
{
    private static readonly Lazy<BluettiProductSearch> Catalog = new(() => new());
    private readonly string[][] fields;
    private readonly Dictionary<string, List<int>> postings = new(StringComparer.Ordinal);

    private BluettiProductSearch()
    {
        fields = ShippingSkuCatalog.Entries.Select(row => new[]
        {
            row.Sku, row.Model, row.MaterialNumber, row.Upc, row.Ean, row.Jan,
            row.MaterialName, row.Description, row.Category, row.Version, row.Specification
        }.Select(ShippingSkuCatalog.Normalize).ToArray()).ToArray();

        for (int row = 0; row < fields.Length; row++)
        {
            foreach (string gram in fields[row].SelectMany(Grams).Distinct(StringComparer.Ordinal))
            {
                if (!postings.TryGetValue(gram, out var matches))
                    postings.Add(gram, matches = []);
                matches.Add(row);
            }
        }
    }

    internal static ShippingSkuEntry[] Search(string query) => Catalog.Value.Find(query);

    private ShippingSkuEntry[] Find(string query)
    {
        string normalized = ShippingSkuCatalog.Normalize(query);
        var terms = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        List<int>? candidates = null;
        foreach (string gram in terms.SelectMany(Grams).Distinct(StringComparer.Ordinal))
        {
            if (!postings.TryGetValue(gram, out var matches))
                return [];
            if (candidates is null || matches.Count < candidates.Count)
                candidates = matches;
        }

        IEnumerable<int> rows = candidates is null ? Enumerable.Range(0, fields.Length) : candidates;
        return rows.Where(row => terms.All(term =>
                fields[row].Any(field => field.Contains(term, StringComparison.Ordinal))))
            .OrderByDescending(row => fields[row][0] == normalized)
            .ThenByDescending(row => fields[row].Take(6).Contains(normalized, StringComparer.Ordinal))
            .Select(row => ShippingSkuCatalog.Entries[row]).ToArray();
    }

    private static IEnumerable<string> Grams(string text)
    {
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (!char.IsWhiteSpace(text[i]) && !char.IsWhiteSpace(text[i + 1]))
                yield return text.Substring(i, 2);
        }
    }
}
