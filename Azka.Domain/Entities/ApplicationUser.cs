using Microsoft.AspNetCore.Identity;

namespace Azka.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; }
}
