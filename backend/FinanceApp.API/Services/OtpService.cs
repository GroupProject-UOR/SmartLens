using FinanceApp.API.Data;
using FinanceApp.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FinanceApp.API.Services;

public class OtpService
{
    private readonly AppDbContext _context;
    private readonly byte[] _hmacKey;

    public OtpService(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;

        var key = configuration["Otp:HmacKey"]
            ?? throw new InvalidOperationException(
                "OTP HMAC key is not configured.");

        _hmacKey = Convert.FromBase64String(key);

        if (_hmacKey.Length < 32)
        {
            throw new InvalidOperationException(
                "OTP HMAC key must contain at least 32 bytes.");
        }
    }

    public async Task<string> GenerateOtpAsync(
        string userId,
        string verificationMethod)
    {
        verificationMethod = verificationMethod.ToLowerInvariant();

        if (verificationMethod is not ("email" or "phone"))
        {
            throw new ArgumentException(
                "Invalid verification method.");
        }

        var now = DateTime.UtcNow;

        // Find the most recent OTP request.
        var latestOtp = await _context.OtpVerifications
            .Where(o =>
                o.UserId == userId &&
                o.VerificationMethod == verificationMethod)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        // Prevent rapid OTP requests.
        if (latestOtp != null &&
            now - latestOtp.CreatedAt < TimeSpan.FromSeconds(60))
        {
            throw new InvalidOperationException(
                "Please wait before requesting another code.");
        }

        // Generate a cryptographically secure six-digit code.
        var otp = RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");

        // Invalidate any previous unused OTPs.
        var previousOtps = await _context.OtpVerifications
            .Where(o =>
                o.UserId == userId &&
                o.VerificationMethod == verificationMethod &&
                !o.IsUsed)
            .ToListAsync();

        foreach (var previous in previousOtps)
        {
            previous.IsUsed = true;
        }

        var verification = new OtpVerification
        {
            UserId = userId,
            VerificationMethod = verificationMethod,
            CodeHash = ComputeHash(userId, verificationMethod, otp),
            ExpiresAt = now.AddMinutes(5),
            CreatedAt = now,
            FailedAttempts = 0,
            IsUsed = false
        };

        _context.OtpVerifications.Add(verification);

        await _context.SaveChangesAsync();

        // Return the code to the trusted caller for delivery.
        // Never return it from a production API response.
        return otp;
    }

    public async Task<bool> VerifyOtpAsync(
        ApplicationUser user,
        string verificationMethod,
        string code)
    {
        verificationMethod = verificationMethod.ToLowerInvariant();

        if (verificationMethod is not ("email" or "phone"))
        {
            return false;
        }

        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        var now = DateTime.UtcNow;

        var verification = await _context.OtpVerifications
            .Where(o =>
                o.UserId == user.Id &&
                o.VerificationMethod == verificationMethod &&
                !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (verification == null ||
            verification.ExpiresAt <= now ||
            verification.FailedAttempts >= 5)
        {
            return false;
        }

        var suppliedHash = ComputeHash(
            user.Id,
            verificationMethod,
            code);

        var expected = Convert.FromHexString(
            verification.CodeHash);

        var supplied = Convert.FromHexString(
            suppliedHash);

        if (!CryptographicOperations.FixedTimeEquals(
                expected, supplied))
        {
            verification.FailedAttempts++;
            await _context.SaveChangesAsync();
            return false;
        }

        // Mark the code as used.
        verification.IsUsed = true;

        // Update the corresponding Identity confirmation flag.
        if (verificationMethod == "email")
        {
            user.EmailConfirmed = true;
        }
        else
        {
            user.PhoneNumberConfirmed = true;
        }

        await _context.SaveChangesAsync();

        return true;
    }

    private string ComputeHash(
        string userId,
        string method,
        string code)
    {
        using var hmac = new HMACSHA256(_hmacKey);

        var input = Encoding.UTF8.GetBytes(
            $"{userId}:{method}:{code}");

        return Convert.ToHexString(
            hmac.ComputeHash(input));
    }
}