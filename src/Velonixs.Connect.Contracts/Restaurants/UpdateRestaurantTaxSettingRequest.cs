using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Contracts.Restaurants;

public sealed record UpdateRestaurantTaxSettingRequest(
    [Range(typeof(decimal), "0", "100")]
    decimal CgstPercent,

    [Range(typeof(decimal), "0", "100")]
    decimal SgstPercent);
