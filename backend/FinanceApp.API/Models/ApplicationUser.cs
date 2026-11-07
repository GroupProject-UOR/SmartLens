using Microsoft.AspNetCore.Identity;

namespace FinanceApp.API.Models;

public class ApplicationUser : IdentityUser
{
    public DateOnly DateOfBirth { get; set; }
}