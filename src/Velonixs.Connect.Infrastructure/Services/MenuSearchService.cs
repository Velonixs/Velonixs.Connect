using System.Text.RegularExpressions;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class MenuSearchService
{
    public IReadOnlyList<MenuSearchMatch> Search(
        string query,
        IEnumerable<MenuItem> activeItems,
        int limit = 9)
    {
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length < 2)
        {
            return Array.Empty<MenuSearchMatch>();
        }

        return activeItems
            .Where(item => item.IsActive && item.IsAvailable)
            .Select(item => new MenuSearchMatch(item, Score(normalizedQuery, Normalize(item.Name))))
            .Where(match => match.Score >= 0.45)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Item.Name)
            .Take(limit)
            .ToArray();
    }

    public static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    private static double Score(string query, string itemName)
    {
        if (query == itemName)
        {
            return 1;
        }

        if (itemName.Contains(query, StringComparison.Ordinal) ||
            query.Contains(itemName, StringComparison.Ordinal))
        {
            return 0.9;
        }

        var queryTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nameTokens = itemName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = queryTokens.Intersect(nameTokens).Count();

        return overlap == 0
            ? 0
            : (double)overlap / Math.Max(queryTokens.Length, nameTokens.Length);
    }
}

public sealed record MenuSearchMatch(MenuItem Item, double Score);
