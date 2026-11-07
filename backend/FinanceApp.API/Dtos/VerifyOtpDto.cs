using System.ComponentModel.DataAnnotations;

namespace FinanceApp.API.DTOs;

public class VerifyOtpDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string VerificationMethod { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9]{6}$")]
    public string Code { get; set; } = string.Empty;
}