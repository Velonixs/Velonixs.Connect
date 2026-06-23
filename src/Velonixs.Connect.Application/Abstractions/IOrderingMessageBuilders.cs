using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Application.Abstractions;

/// <summary>Builds category navigation messages for an ordering channel.</summary>
public interface ICategoryMessageBuilder
{
    int PageSize { get; }

    IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSections(
        IReadOnlyList<MenuCategory> categories,
        int page,
        bool hasCart);
}

/// <summary>Builds menu-item browsing and search messages.</summary>
public interface IMenuMessageBuilder
{
    int PageSize { get; }

    IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSections(
        Guid categoryId,
        IReadOnlyList<MenuItem> items,
        int page,
        bool hasCart);

    IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSearchSections(
        IReadOnlyList<MenuItem> items);
}

/// <summary>Builds quantity selection messages while preserving menu navigation context.</summary>
public interface IQuantityMessageBuilder
{
    IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSections(MenuItem item);
}

/// <summary>Builds cart and checkout navigation messages.</summary>
public interface ICartNavigationMessageBuilder
{
    IReadOnlyCollection<WhatsAppReplyButton> BuildMainMenuButtons();
    IReadOnlyCollection<WhatsAppReplyButton> BuildCartButtons();
    IReadOnlyCollection<WhatsAppReplyButton> BuildFulfilmentButtons();
}
