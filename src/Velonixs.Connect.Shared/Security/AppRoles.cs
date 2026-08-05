namespace Velonixs.Connect.Shared.Security;

public static class AppRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string BusinessOwner = "BusinessOwner";
    public const string BusinessManager = "BusinessManager";
    public const string Cashier = "Cashier";
    public const string Staff = "Staff";

    public const string PortalRoles =
        BusinessOwner + "," + BusinessManager + "," + Cashier + "," + Staff;

    public static readonly string[] PortalRoleNames =
    [
        BusinessOwner,
        BusinessManager,
        Cashier,
        Staff
    ];

    public static readonly IReadOnlyCollection<string> All =
    [
        PlatformAdmin,
        BusinessOwner,
        BusinessManager,
        Cashier,
        Staff
    ];

    public static readonly IReadOnlyCollection<string> StaffAssignable =
    [
        BusinessManager,
        Cashier,
        Staff
    ];
}
