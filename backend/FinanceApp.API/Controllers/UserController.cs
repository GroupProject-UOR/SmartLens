using FinanceApp.API.DTOs;
using FinanceApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(
    UserManager<ApplicationUser> userManager
) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        // Get the user ID from the JWT's "sub" claim
        var userId = User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user token"
            });
        }

        // Find the user in the database
        var user = await userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found"
            });
        }

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.PhoneNumber,
            user.DateOfBirth,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed
        });
    }
    [HttpGet("test")]
    [AllowAnonymous]
    public IActionResult Test()
    {
        return Ok(new
        {
            message = "UsersController is working"
        });
    }

    [HttpGet("auth-test")]
    public IActionResult AuthTest()
    {
        return Ok(new
        {
            message = "JWT authentication successful",
            isAuthenticated = User.Identity?.IsAuthenticated,
            claims = User.Claims.Select(c => new
            {
                type = c.Type,
                value = c.Value
            })
        });
    }
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateProfileDto dto)
    {
        // Get the logged-in user's ID from the JWT
        var userId = User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user token"
            });
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found"
            });
        }

        var username = dto.Username.Trim();

        // Check whether another user already has this username
        var existingUser = await userManager.FindByNameAsync(username);

        if (existingUser != null && existingUser.Id != user.Id)
        {
            return Conflict(new
            {
                message = "Username is already taken."
            });
        }

        user.UserName = username;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new
        {
            message = "Profile updated successfully.",
            user = new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.PhoneNumber,
                user.DateOfBirth
            }
        });
    }

}