using System.Security.Claims;
using MedicalData.Api.Data;
using MedicalData.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Controllers;

[Route("api/profile")]
[ApiController]
[Authorize]
public class UserProfileController(AppDbContext context) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> Get()
    {
        var uid = CurrentUserId;
        var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        if (profile == null)
            return Ok(new UserProfileDto());

        return Ok(new UserProfileDto
        {
            Sex      = profile.Sex,
            BirthDate = profile.BirthDate.HasValue
                ? profile.BirthDate.Value.ToString("yyyy-MM-dd")
                : null,
            CycleDay = profile.CycleDay,
        });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] UserProfileDto dto)
    {
        var uid = CurrentUserId;
        var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == uid);

        if (profile == null)
        {
            profile = new UserProfile { UserId = uid };
            context.UserProfiles.Add(profile);
        }

        profile.Sex      = dto.Sex ?? "";
        profile.BirthDate = DateTime.TryParse(dto.BirthDate, out var bd) ? bd.ToUniversalTime() : null;
        profile.CycleDay  = dto.CycleDay is > 0 and <= 28 ? dto.CycleDay : null;

        await context.SaveChangesAsync();
        return NoContent();
    }
}

public class UserProfileDto
{
    public string? Sex { get; set; }
    public string? BirthDate { get; set; }
    public int? CycleDay { get; set; }
}
