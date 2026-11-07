using System.ComponentModel.DataAnnotations;

namespace FinanceApp.API.DTOs;

public class SendOtpDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;
}