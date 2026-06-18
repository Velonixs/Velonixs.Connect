using Microsoft.AspNetCore.Identity;

namespace Velonixs.Connect.Persistence.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? BusinessId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Domain.Entities.Restaurant? Business { get; set; }
}
