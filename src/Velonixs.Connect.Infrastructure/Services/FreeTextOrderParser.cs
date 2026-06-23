using System.Text.RegularExpressions;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed partial class FreeTextOrderParser(MenuSearchService menuSearchService)
{
    public FreeTextOrderParseResult Parse(string text, IReadOnlyCollection<MenuItem> activeItems)
    {
        var cleaned = PreambleRegex().Replace(text.Trim(), string.Empty);
        var parts = SeparatorRegex().Split(cleaned)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        var parsed = new List<ParsedNaturalOrderItem>();
        var ambiguous = new List<MenuItem>();

        foreach (var part in parts)
        {
            var match = QuantityAndNameRegex().Match(part.Trim());
            if (!match.Success ||
                !int.TryParse(match.Groups["quantity"].Value, out var quantity) ||
                quantity <= 0)
            {
                return new FreeTextOrderParseResult(false, parsed, ambiguous);
            }

            var query = match.Groups["name"].Value.Trim();
            var matches = menuSearchService.Search(query, activeItems, 5);
            if (matches.Count == 0)
            {
                return new FreeTextOrderParseResult(false, parsed, ambiguous);
            }

            if (matches[0].Score >= 0.9 &&
                (matches.Count == 1 || matches[0].Score - matches[1].Score >= 0.15))
            {
                parsed.Add(new ParsedNaturalOrderItem(matches[0].Item, quantity));
                continue;
            }

            ambiguous.AddRange(matches.Select(x => x.Item));
        }

        return new FreeTextOrderParseResult(
            parsed.Count > 0 && ambiguous.Count == 0,
            parsed,
            ambiguous.DistinctBy(x => x.Id).Take(9).ToArray());
    }

    [GeneratedRegex(@"^(?:i\s+(?:need|want|would like)|please|order)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex PreambleRegex();

    [GeneratedRegex(@"\s*(?:,|&|\band\b)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"^(?<quantity>\d+)\s*(?:x\s*)?(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityAndNameRegex();
}

public sealed record ParsedNaturalOrderItem(MenuItem Item, int Quantity);

public sealed record FreeTextOrderParseResult(
    bool IsHighConfidence,
    IReadOnlyCollection<ParsedNaturalOrderItem> Items,
    IReadOnlyCollection<MenuItem> SuggestedItems);
