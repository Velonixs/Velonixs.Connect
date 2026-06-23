namespace Velonixs.Connect.Domain.Entities;

public static class ConversationStates
{
    public const string New = "NEW";
    public const string MenuSent = "MENU_SENT";
    public const string CategorySelection = "CATEGORY_SELECTION";
    public const string ItemSelection = "ITEM_SELECTION";
    public const string QuantitySelection = "QUANTITY_SELECTION";
    public const string SearchSelection = "SEARCH_SELECTION";
    public const string OrderInputPending = "ORDER_INPUT_PENDING";
    public const string OrderReceived = "ORDER_RECEIVED";
    public const string CartReview = "CART_REVIEW";
    public const string CustomerNamePending = "CUSTOMER_NAME_PENDING";
    public const string FulfilmentPending = "FULFILMENT_PENDING";
    public const string AddressPending = "ADDRESS_PENDING";
    public const string ConfirmationPending = "CONFIRMATION_PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string Cancelled = "CANCELLED";
    public const string StaffHandover = "STAFF_HANDOVER";
}
