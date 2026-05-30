using System.Security.Claims;
using MedicalData.Api.Data;
using MedicalData.Api.Models;
using MedicalData.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] string returnUrl = "/")
    {
        var redirectUri = Url.Action(nameof(GoogleDone), new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-done")]
    public async Task<IActionResult> GoogleDone([FromQuery] string returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync("TempCookie");
        if (!result.Succeeded)
            return Redirect($"{returnUrl}?error=auth_failed");

        var googleId = result.Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var email    = result.Principal.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var name     = result.Principal.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var picture  = result.Principal.FindFirst("picture")?.Value;

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
        if (user is null)
        {
            user = new User { GoogleId = googleId, Email = email, Name = name, PictureUrl = picture };
            db.Users.Add(user);
            await db.SaveChangesAsync(); // user must exist in DB before FK update

            await db.LabResults
                .Where(r => r.UserId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.UserId, user.Id));
        }
        else
        {
            user.Name = name;
            user.PictureUrl = picture;
            await db.SaveChangesAsync();
        }
        await HttpContext.SignOutAsync("TempCookie");

        var token = jwt.GenerateToken(user);
        return Redirect($"{returnUrl}?token={Uri.EscapeDataString(token)}");
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserDto> Me() => Ok(new UserDto
    {
        Id         = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",
        Email      = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
        Name       = User.FindFirst(ClaimTypes.Name)?.Value ?? "",
        PictureUrl = User.FindFirst("picture")?.Value,
    });
}

public class UserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? PictureUrl { get; set; }
}
