namespace Velonixs.Connect.Domain.Entities;

public static class BusinessTypes
{
    public const string Restaurant = "Restaurant";
    public const string GroceryStore = "GroceryStore";
    public const string FishMarket = "FishMarket";
    public const string Bakery = "Bakery";
    public const string SweetShop = "SweetShop";
    public const string Pharmacy = "Pharmacy";
    public const string RetailStore = "RetailStore";

    public static readonly string[] All =
    [
        Restaurant,
        GroceryStore,
        FishMarket,
        Bakery,
        SweetShop,
        Pharmacy,
        RetailStore
    ];

    public static string Normalize(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Restaurant;
}
