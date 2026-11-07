using FinanceApp.API.DTOs;
using FinanceApp.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FinanceApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    OtpService otpService,
    DevelopmentOtpSender otpSender,
    JwtService jwtService
) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        // Validate date of birth
        if (!dto.DateOfBirth.HasValue ||
            dto.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(new
            {
                message = "Invalid date of birth"
            });
        }

        // Validate verification method
        var method = dto.VerificationMethod?.Trim().ToLowerInvariant();

        if (method is not ("email" or "phone"))
        {
            return BadRequest(new
            {
                message = "Verification method must be email or phone."
            });
        }

        if (method != "email" && method != "phone")
        {
            return BadRequest(new
            {
                message = "Verification method must be email or phone"
            });
        }

        if (method == "email" && string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new
            {
                message = "Email is required"
            });
        }

        if (method == "phone" && string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            return BadRequest(new
            {
                message = "Phone number is required"
            });
        }

        string? normalizedPhone = null;

        if (method == "phone")
        {
            normalizedPhone =
                PhoneNumberHelper.NormalizeSriLankanNumber(dto.PhoneNumber);

            if (normalizedPhone == null)
            {
                return BadRequest(new
                {
                    message = "Invalid Sri Lankan phone number."
                });
            }
        }

        if (method == "phone")
        {
            var existingPhone = await userManager.Users
                .AnyAsync(u => u.PhoneNumber == normalizedPhone);

            if (existingPhone)
            {
                return Conflict(new
                {
                    message = "Phone number is already registered."
                });
            }
        }

        // Check username
        var username = dto.Username.Trim();

        var existingUser = await userManager.FindByNameAsync(username);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "Username is already taken."
            });
        }



        // Check email
        string? normalizedEmail = null;

        if (method == "email")
        {
            normalizedEmail = dto.Email!.Trim();

            var existingEmail =
                await userManager.FindByEmailAsync(normalizedEmail);

            if (existingEmail != null)
            {
                return Conflict(new
                {
                    message = "Email is already registered."
                });
            }
        }

        // Create user
        var user = new ApplicationUser
        {
            UserName = username,
            DateOfBirth = dto.DateOfBirth.Value,
            Email = method == "email" ? normalizedEmail : null,
            PhoneNumber = method == "phone" ? normalizedPhone : null,
            EmailConfirmed = false,
            PhoneNumberConfirmed = false
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return StatusCode(201, new
        {
            message = "Registration started. Verification is required.",
            userId = user.Id,
            verificationMethod = method
        });
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode(SendOtpDto dto)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Invalid verification request."
            });
        }

        string method;
        string destination;

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            method = "email";
            destination = user.Email;

            if (user.EmailConfirmed)
            {
                return BadRequest(new
                {
                    message = "Account is already verified."
                });
            }
        }
        else if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            method = "phone";
            destination = user.PhoneNumber;

            if (user.PhoneNumberConfirmed)
            {
                return BadRequest(new
                {
                    message = "Account is already verified."
                });
            }
        }
        else
        {
            return BadRequest(new
            {
                message = "No verification contact is configured."
            });
        }

        try
        {
            var code = await otpService.GenerateOtpAsync(
                user.Id,
                method);

            await otpSender.SendAsync(destination, code);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new
            {
                message = "Unable to send code. Please wait before trying again."
            });
        }

        return Ok(new
        {
            message = "Verification code requested."
        });
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode(VerifyOtpDto dto)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Invalid or expired verification code."
            });
        }

        var method = dto.VerificationMethod.ToLowerInvariant();

        if (method != "email" && method != "phone")
        {
            return BadRequest(new
            {
                message = "Invalid verification method."
            });
        }

        if ((method == "email" && string.IsNullOrWhiteSpace(user.Email)) ||
            (method == "phone" && string.IsNullOrWhiteSpace(user.PhoneNumber)))
        {
            return BadRequest(new
            {
                message = "Invalid verification request."
            });
        }

        var verified = await otpService.VerifyOtpAsync(
            user,
            method,
            dto.Code);

        if (!verified)
        {
            return BadRequest(new
            {
                message = "Invalid or expired verification code."
            });
        }

        return Ok(new
        {
            message = "Account verified successfully."
        });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // 1. Find user by username
        var user = await userManager.FindByNameAsync(dto.Username);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        // 2. Check whether the account is locked
        if (await userManager.IsLockedOutAsync(user))
        {
            return StatusCode(429, new
            {
                message = "Account temporarily locked. Please try again later."
            });
        }

        // 3. Verify password
        var isPasswordValid =
            await userManager.CheckPasswordAsync(user, dto.Password);

        if (!isPasswordValid)
        {
            // Record a failed login attempt
            var failureResult = await userManager.AccessFailedAsync(user);

            if (!failureResult.Succeeded)
            {
                return StatusCode(500, new
                {
                    message = "Unable to process login."
                });
            }

            // Check if the account has now been locked
            if (await userManager.IsLockedOutAsync(user))
            {
                return StatusCode(429, new
                {
                    message = "Account temporarily locked. Please try again later."
                });
            }

            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        // 4. Require at least one verified contact method
        var isVerified =
            (!string.IsNullOrWhiteSpace(user.Email) && user.EmailConfirmed)
            ||
            (!string.IsNullOrWhiteSpace(user.PhoneNumber) && user.PhoneNumberConfirmed);

        if (!isVerified)
        {
            return StatusCode(403, new
            {
                message = "Please verify your account before logging in."
            });
        }

        // 5. Reset failed login attempts after successful authentication
        var resetResult = await userManager.ResetAccessFailedCountAsync(user);

        if (!resetResult.Succeeded)
        {
            return StatusCode(500, new
            {
                message = "Unable to process login."
            });
        }

        // 6. Generate JWT
        var token = jwtService.GenerateToken(user);

        return Ok(new
        {
            message = "Login successful.",
            accessToken = token,
            tokenType = "Bearer",
            expiresIn = 3600,
            user = new
            {
                id = user.Id,
                username = user.UserName,
                email = user.Email,
                phoneNumber = user.PhoneNumber
            }
        });
    }
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            id = user.Id,
            username = user.UserName,
            email = user.Email,
            phoneNumber = user.PhoneNumber,
            dateOfBirth = user.DateOfBirth,
            emailConfirmed = user.EmailConfirmed,
            phoneNumberConfirmed = user.PhoneNumberConfirmed
        });
    }
}