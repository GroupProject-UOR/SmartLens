namespace FinanceApp.API.Services;

public class DevelopmentOtpSender
{
    private readonly ILogger<DevelopmentOtpSender> _logger;
    private readonly IWebHostEnvironment _environment;

    public DevelopmentOtpSender(
        ILogger<DevelopmentOtpSender> logger,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendAsync(
        string destination,
        string code)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "A production OTP delivery provider is not configured.");
        }

        _logger.LogWarning(
            "DEVELOPMENT ONLY - OTP for {Destination}: {Code}",
            destination,
            code);

        return Task.CompletedTask;
    }
}