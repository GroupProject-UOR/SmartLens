namespace FinanceApp.API.Models;

public class OtpVerification
{
    public int Id { get; set; }

    // User who requested verification
    public string UserId { get; set; } = string.Empty;

    // Navigation property
    public ApplicationUser User { get; set; } = null!;

    // "email" or "phone"
    public string VerificationMethod { get; set; } = string.Empty;

    // Store a protected hash, never the actual OTP
    public string CodeHash { get; set; } = string.Empty;

    // When the OTP expires
    public DateTime ExpiresAt { get; set; }

    // Number of incorrect verification attempts
    public int FailedAttempts { get; set; } = 0;

    // Whether this OTP has been used
    public bool IsUsed { get; set; } = false;

    // When the OTP was generated
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}