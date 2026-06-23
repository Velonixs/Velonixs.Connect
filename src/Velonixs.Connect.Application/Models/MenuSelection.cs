namespace Velonixs.Connect.Application.Models;

/// <summary>
/// Represents menu browsing and selection context independently of a specific user interface.
/// It can be reused by WhatsApp lists, WhatsApp Flows, and web ordering experiences.
/// </summary>
public sealed class MenuSelection
{
    public Guid? CurrentCategoryId { get; set; }
    public string? CurrentCategoryName { get; set; }
    public MenuSelectionItem? LastSelectedMenuItem { get; set; }
    public int CurrentMenuPage { get; set; }
    public Guid? CartId { get; set; }
    public List<MenuSelectionItem> SelectedItems { get; set; } = new();
}

/// <summary>
/// Describes a selected menu item without coupling selection state to persistence entities.
/// </summary>
public sealed class MenuSelectionItem
{
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
