using System.Text.RegularExpressions;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

/// <summary>
/// Provides deterministic, tenant-scoped menu matching. The caller supplies the
/// tenant's menu items; this service never queries or combines tenant data.
/// </summary>
public sealed class MenuSearchService
{
    public IReadOnlyList<MenuSearchMatch> Search(
        string? query,
        IEnumerable<MenuItem> activeItems,
        int limit = 9)
    {
        ArgumentNullException.ThrowIfNull(activeItems);

        var normalizedQuery = Normalize(query ?? string.Empty);
        if (normalizedQuery.Length == 0 || limit <= 0)
        {
            return Array.Empty<MenuSearchMatch>();
        }

        return activeItems
            .Where(item => item.IsActive && item.IsAvailable)
            .Select(item => new MenuSearchMatch(item, Score(normalizedQuery, item)))
            .Where(match => match.Score >= 0.45)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Item.ItemCode)
            .ThenBy(match => match.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    private static double Score(string query, MenuItem item)
    {
        var name = Normalize(item.Name);
        var itemCode = item.ItemCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var retailerId = Normalize(item.ProductRetailerId ?? string.Empty);
        var category = Normalize(item.Category?.Name ?? string.Empty);
        var description = Normalize(item.Description ?? string.Empty);
        var codeQuery = query.StartsWith("code ", StringComparison.Ordinal)
            ? query["code ".Length..].Trim()
            : query;

        // A product code is an unambiguous, intentional lookup and gets the
        // same rank as an exact product name.
        if (codeQuery == itemCode)
        {
            return 1.00;
        }

        if (query == name)
        {
            return 1.00;
        }

        if (name.StartsWith(query, StringComparison.Ordinal))
        {
            return 0.95;
        }

        if (name.Contains(query, StringComparison.Ordinal))
        {
            return 0.90;
        }

        var nameTokenScore = ScoreTokenOverlap(query, name);
        if (nameTokenScore >= 0.75)
        {
            return nameTokenScore;
        }

        if (!string.IsNullOrEmpty(retailerId) && retailerId.Contains(query, StringComparison.Ordinal))
        {
            return 0.65;
        }

        if (!string.IsNullOrEmpty(category) && category.Contains(query, StringComparison.Ordinal))
        {
            return 0.65;
        }

        if (IsVegetarianQuery(query) && item.IsVegetarian)
        {
            return 0.65;
        }

        if (!string.IsNullOrEmpty(description) && description.Contains(query, StringComparison.Ordinal))
        {
            return 0.50;
        }

        return 0;
    }

    private static double ScoreTokenOverlap(string query, string name)
    {
        var queryTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nameTokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (queryTokens.Length == 0 || nameTokens.Length == 0)
        {
            return 0;
        }

        var overlap = queryTokens.Intersect(nameTokens, StringComparer.Ordinal).Count();
        return overlap == 0 ? 0 : (double)overlap / queryTokens.Length;
    }

    private static bool IsVegetarianQuery(string query) =>
        query is "veg" or "vegetarian" or "vegan";
}

public sealed record MenuSearchMatch(MenuItem Item, double Score);
