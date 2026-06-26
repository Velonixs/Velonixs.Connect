using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Contracts.Orders;

public sealed record RejectOrderRequest(
    [Required]
    string Reason);
