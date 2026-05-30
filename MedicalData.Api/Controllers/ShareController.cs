using System.Security.Claims;
using MedicalData.Api.Data;
using MedicalData.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Controllers;

[Route("api/share")]
[ApiController]
public class ShareController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ShareLinkDto>> CreateShareLink(
        [FromBody] CreateShareRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var shareToken = new ShareToken
        {
            Token     = Guid.NewGuid().ToString("N"),
            UserId    = userId,
            Scope     = request.Scope ?? "all",
            ExpiresAt = request.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
                : null,
        };

        db.ShareTokens.Add(shareToken);
        await db.SaveChangesAsync(ct);

        var clientBase = config["App:ClientBaseUrl"]?.TrimEnd('/') ?? "";
        return Ok(new ShareLinkDto
        {
            Token = shareToken.Token,
            Url   = $"{clientBase}/shared/{shareToken.Token}",
            Scope = shareToken.Scope,
            ExpiresAt = shareToken.ExpiresAt,
        });
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<SharedDataDto>> GetSharedData(string token, CancellationToken ct)
    {
        var shareToken = await db.ShareTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (shareToken is null) return NotFound("Посилання не знайдено.");
        if (shareToken.ExpiresAt.HasValue && shareToken.ExpiresAt < DateTime.UtcNow)
            return Gone("Термін дії посилання закінчився.");

        IQueryable<LabResult> query = db.LabResults.Where(r => r.UserId == shareToken.UserId);

        query = shareToken.Scope switch
        {
            "latest" => query.Where(r => r.TestDate == query.Max(x => x.TestDate)),
            _        => query
        };

        var results = await query
            .OrderBy(r => r.TestDate).ThenBy(r => r.TestName)
            .Select(r => new SharedLabResultDto
            {
                TestName       = r.TestName,
                Value          = r.Value,
                Unit           = r.Unit,
                ReferenceRange = r.ReferenceRange,
                TestDate       = r.TestDate,
                IsAbnormal     = r.IsAbnormal,
            })
            .ToListAsync(ct);

        return Ok(new SharedDataDto
        {
            OwnerName = shareToken.User.Name,
            Scope     = shareToken.Scope,
            ExpiresAt = shareToken.ExpiresAt,
            Results   = results,
        });
    }

    private ObjectResult Gone(string message) =>
        StatusCode(StatusCodes.Status410Gone, message);
}

public class CreateShareRequest
{
    public string Scope { get; set; } = "all";
    public int? ExpiresInDays { get; set; }
}

public class ShareLinkDto
{
    public string Token { get; set; } = "";
    public string Url { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}

public class SharedDataDto
{
    public string OwnerName { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public List<SharedLabResultDto> Results { get; set; } = [];
}

public class SharedLabResultDto
{
    public string TestName       { get; set; } = "";
    public double Value          { get; set; }
    public string Unit           { get; set; } = "";
    public string ReferenceRange { get; set; } = "";
    public DateTime TestDate     { get; set; }
    public bool IsAbnormal       { get; set; }
}
