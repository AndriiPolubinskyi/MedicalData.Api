using System.Security.Claims;
using System.Text.RegularExpressions;
using MedicalData.Api.Data;
using MedicalData.Api.Dto.Requests;
using MedicalData.Api.Dto.Responses;
using MedicalData.Api.Models;
using MedicalData.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LabResultController(AppDbContext context, ILabPdfAgentParser pdfAgentParser, ILabAiService? labAiService = null) : ControllerBase
{
    private const long MaxPdfSizeBytes = 5 * 1024 * 1024;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LabResult>>> GetAll()
    {
        var uid = CurrentUserId;
        return await context.LabResults.Where(r => r.UserId == uid).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LabResult>> GetItem(int id)
    {
        var uid = CurrentUserId;
        var labResult = await context.LabResults.FirstOrDefaultAsync(r => r.Id == id && r.UserId == uid);
        if (labResult == null) return NotFound();
        return labResult;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = CurrentUserId;
        var labResult = await context.LabResults.FirstOrDefaultAsync(r => r.Id == id && r.UserId == uid);
        if (labResult == null) return NotFound();

        context.LabResults.Remove(labResult);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("by-date")]
    public async Task<IActionResult> DeleteByDate([FromQuery] string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var d)) return BadRequest("Invalid date.");
        var uid   = CurrentUserId;
        var start = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
        var end   = start.AddDays(1);
        var deleted = await context.LabResults
            .Where(r => r.UserId == uid && r.TestDate >= start && r.TestDate < end)
            .ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    [HttpGet("labtypes")]
    public async Task<ActionResult<IEnumerable<string>>> GetAllTypes()
    {
        var uid = CurrentUserId;
        return await context.LabResults
            .Where(r => r.UserId == uid)
            .GroupBy(x => x.TestName).Select(x => x.Key).ToListAsync();
    }

    [HttpGet("labresults")]
    public async Task<ActionResult<IEnumerable<LabResult>>> GetAllResultsByTestName(string testName)
    {
        var uid = CurrentUserId;
        return await context.LabResults
            .Where(x => x.TestName == testName && x.UserId == uid)
            .OrderBy(x => x.TestDate).ToListAsync();
    }
    
    [HttpPost("by-date")]
    public async Task<ActionResult> UploadByDate([FromBody] UploadByDateRequest request, CancellationToken cancellationToken)
    {
        var metrics = request.Results.Select(x => new ParsedLabMetric
        {
            TestName = x.Key,
            Value = x.Value
        });

        var savedCount = await SaveResultsByDateAsync(request.Date, metrics, cancellationToken, CurrentUserId);
        return Ok(new { savedCount });
    }

    [HttpPost("upload-pdf")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxPdfSizeBytes)]
    public async Task<ActionResult<UploadPdfLabResultsResponse>> UploadPdf(
        IFormFile file,
        [FromForm] DateTime? date = null,
        [FromForm] bool persist = true,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("PDF file is required.");
        }

        if (file.Length > MaxPdfSizeBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "File is too large.");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, "Only .pdf files are supported.");
        }

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        ParsedLabPdfDocument parsedDocument;
        try
        {
            parsedDocument = await pdfAgentParser.ParseAsync(memoryStream, cancellationToken);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, "Failed to parse the PDF content.");
        }

        if (parsedDocument.Results.Count == 0)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, "No lab results found in PDF.");
        }

        var targetDate = date?.Date ?? parsedDocument.Date ?? DateTime.UtcNow.Date;
        var savedCount = persist
            ? await SaveResultsByDateAsync(targetDate, parsedDocument.Results, cancellationToken, CurrentUserId)
            : 0;

        var response = new UploadPdfLabResultsResponse
        {
            Date = targetDate,
            SavedCount = savedCount,
            Results = parsedDocument.Results.Select(x => new ParsedLabMetricResponse
            {
                TestName = x.TestName,
                Value = x.Value,
                Unit = x.Unit,
                ReferenceRange = x.ReferenceRange,
                IsAbnormal = x.IsAbnormal
            }).ToList()
        };

        return Ok(response);
    }
    
    [HttpPost("save-parsed")]
    public async Task<ActionResult> SaveParsed([FromBody] SaveParsedRequest request, CancellationToken cancellationToken)
    {
        var metrics = request.Results.Select(x => new ParsedLabMetric
        {
            TestName = x.TestName,
            Value = x.Value,
            Unit = x.Unit,
            ReferenceRange = x.ReferenceRange,
            IsAbnormal = x.IsAbnormal,
        });

        var savedCount = await SaveResultsByDateAsync(request.Date, metrics, cancellationToken, CurrentUserId);
        return Ok(new { savedCount });
    }

    [HttpPost("ai-summary")]
    public async Task<ActionResult> GetAiSummary(
        [FromBody] AiSummaryRequest request,
        CancellationToken cancellationToken)
    {
        if (labAiService is null)
            return StatusCode(503, "AI service not configured.");

        var items = request.Results.Select(x => new AiSummaryItem
        {
            TestName = x.TestName,
            Value = x.Value,
            Unit = x.Unit,
            ReferenceRange = x.ReferenceRange,
            IsAbnormal = x.IsAbnormal,
        }).ToList();

        try
        {
            var summary = await labAiService.GetSummaryAsync(request.Date, items, cancellationToken);
            return Ok(new { summary });
        }
        catch (Exception)
        {
            return StatusCode(503, "Failed to generate AI summary.");
        }
    }

    [HttpPost("update_text")]
    public async Task<ActionResult<LabResult>> UpdateText()
    {
        var labResults = await context.LabResults.ToListAsync();
        foreach (var result in labResults)
        {
            if (result.TestName.Contains("\n"))
            {
                result.TestName = result.TestName.Replace("\n", " ");
            }
        }
        await context.SaveChangesAsync();
        return Ok();
    }

    // ── Test name normalization ───────────────────────
    // Extracts the last short abbreviation from parentheses, e.g.:
    //   "Глюкоза (Glucose, GLU)"  → "GLU"
    //   "Глюкоза (GLU)"           → "GLU"
    //   "Індекс атерогенності"    → null  (no parens)
    private static readonly Regex AbbrRegex = new(@"\(([^)]+)\)", RegexOptions.Compiled);

    private static string? ExtractAbbrKey(string name)
    {
        var matches = AbbrRegex.Matches(name);
        if (matches.Count == 0) return null;

        var lastContent = matches[^1].Groups[1].Value;
        var lastToken = lastContent.Split(',')[^1].Trim();

        // Normalize unicode dashes → ASCII hyphen, strip trailing cholesterol suffix "-C"
        lastToken = lastToken.Replace('–', '-').Replace('—', '-');
        if (lastToken.EndsWith("-C", StringComparison.OrdinalIgnoreCase))
            lastToken = lastToken[..^2];

        return lastToken.Length is >= 2 and <= 12
               && lastToken.All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '%')
            ? lastToken.ToUpperInvariant()
            : null;
    }

    [HttpPost("normalize-names")]
    public async Task<ActionResult> NormalizeNames(CancellationToken cancellationToken)
    {
        // Load all distinct test names
        var allNames = await context.LabResults
            .Select(x => x.TestName)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Group names that share the same abbreviation key
        var groups = allNames
            .Select(n => (Name: n, Key: ExtractAbbrKey(n)))
            .Where(x => x.Key != null)
            .GroupBy(x => x.Key!)
            .Where(g => g.Count() > 1)
            .ToList();

        // Also find names where one is a bare prefix of another: "Індекс атерогенності" vs "Індекс атерогенності (ІА, АІР)"
        var prefixPairs = allNames
            .SelectMany(bare => allNames
                .Where(full => full != bare
                    && full.StartsWith(bare + " (", StringComparison.OrdinalIgnoreCase))
                .Select(full => (Bare: bare, Full: full)))
            .ToList();

        // Merge prefix pairs into the same groups list (use shorter = bare as canonical)
        foreach (var (bare, full) in prefixPairs)
        {
            if (!groups.Any(g => g.Any(x => x.Name == bare) || g.Any(x => x.Name == full)))
                groups.Add(new[] { (Name: bare, Key: (string?)null), (Name: full, Key: (string?)null) }
                    .GroupBy(_ => "__prefix__")
                    .First());
        }

        if (groups.Count == 0)
            return Ok(new { mergedGroups = 0, updatedRows = 0 });

        var totalUpdated = 0;

        foreach (var group in groups)
        {
            // Canonical = shortest name, tie-break alphabetically
            var canonical = group
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .First().Name;

            var duplicates = group
                .Where(x => !string.Equals(x.Name, canonical, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .ToList();

            foreach (var dup in duplicates)
            {
                var rows = await context.LabResults
                    .Where(r => r.TestName == dup)
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                    row.TestName = canonical;

                totalUpdated += rows.Count;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(new { mergedGroups = groups.Count, updatedRows = totalUpdated });
    }

    private async Task<int> SaveResultsByDateAsync(
        DateTime date,
        IEnumerable<ParsedLabMetric> metrics,
        CancellationToken cancellationToken,
        Guid? userId = null)
    {
        var materializedMetrics = metrics
            .Where(x => !string.IsNullOrWhiteSpace(x.TestName))
            .ToList();

        if (materializedMetrics.Count == 0)
            return 0;

        // Build abbreviation key → canonical name map from existing DB names
        var existingNames = await context.LabResults
            .Select(x => x.TestName)
            .Distinct()
            .ToListAsync(cancellationToken);

        var canonicalByKey = existingNames
            .Select(n => (Name: n, Key: ExtractAbbrKey(n)))
            .Where(x => x.Key != null)
            .GroupBy(x => x.Key!)
            .ToDictionary(
                g => g.Key,
                // prefer the shortest name as canonical
                g => g.OrderBy(x => x.Name.Length).ThenBy(x => x.Name).First().Name);

        // Resolve canonical name for each incoming metric
        foreach (var m in materializedMetrics)
        {
            var key = ExtractAbbrKey(m.TestName);
            if (key != null && canonicalByKey.TryGetValue(key, out var canonical))
                m.TestName = canonical;
        }

        var testNames = materializedMetrics
            .Select(x => x.TestName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var knownMetadata = await context.LabResults
            .Where(x => testNames.Contains(x.TestName) && (!string.IsNullOrWhiteSpace(x.Unit) || !string.IsNullOrWhiteSpace(x.ReferenceRange)))
            .Select(x => new { x.TestName, x.Unit, x.ReferenceRange })
            .ToListAsync(cancellationToken);

        var metadataByName = knownMetadata
            .GroupBy(x => x.TestName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var entities = materializedMetrics.Select(metric =>
        {
            metadataByName.TryGetValue(metric.TestName, out var metadata);
            return new LabResult
            {
                TestName = metric.TestName,
                Value = metric.Value,
                TestDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                Unit = string.IsNullOrWhiteSpace(metric.Unit) ? metadata?.Unit ?? string.Empty : metric.Unit,
                ReferenceRange = string.IsNullOrWhiteSpace(metric.ReferenceRange) ? metadata?.ReferenceRange ?? string.Empty : metric.ReferenceRange,
                IsAbnormal = metric.IsAbnormal,
                UserId = userId,
            };
        }).ToList();

        context.LabResults.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);

        return entities.Count;
    }
}


/*
{
"date": "2025-01-25T06:22:07.473Z",
"results": {
    "Глюкоза (Glucose, GLU)": 5.4,
    "Холестерин (Total Blood Cholesterol, ХС, CHOL)": 4.2,
    "Тригліцериди (Triglyceride, ТГ, TG)": 1.0,
    "Холестерин ліпопротеїдів високої щільності (High-density lipoprotein\ncholesterol, Хс.ЛПВЩ, HDL)": 0.73,
    "Холестерин ліпопротеїдів низької щільності (Low-density lipoprotein\ncholesterol, Хс.ЛПНЩ, LDL)": 3.02,
    "Холестерин не-ліпопротеїдів високої щільності (Non-high-density lipoprotein\ncholesterol, Хс.не-ЛПВЩ, Non–HDL-C)": 3.5,
    "Холестерин ліпопротеїдів дуже низької щільності (Very Low Density Lipoprotein,Хс.ЛПДНЩ, VLDL)": 0.45,
    "Індекс атерогенності (ІА, АІР)": 4.8,
    "Інсулін (Insulin)": 16.19
}
}
*/