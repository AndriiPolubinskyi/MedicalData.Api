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
public class LabResultController(AppDbContext context, ILabPdfAgentParser pdfAgentParser, IConfiguration config, ILabAiService? labAiService = null) : ControllerBase
{
    private const long MaxPdfSizeBytes = 5 * 1024 * 1024;

    private bool AiCreditsEnabled =>
        config.GetValue<bool>("Features:AiCreditsEnabled");

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private async Task<ActionResult?> ConsumeAiCreditAsync(CancellationToken ct)
    {
        if (!AiCreditsEnabled) return null;
        var user = await context.Users.FindAsync([CurrentUserId], ct);
        if (user is null) return Unauthorized();
        if (user.Plan == "unlimited") return null;
        if (user.AiCreditsLeft <= 0)
            return StatusCode(402, new { error = "no_credits", creditsLeft = 0 });
        user.AiCreditsLeft--;
        await context.SaveChangesAsync(ct);
        return null;
    }

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

    [HttpGet("labtypes/grouped")]
    public async Task<ActionResult<IEnumerable<object>>> GetGroupedTypes()
    {
        var uid = CurrentUserId;
        var groups = await context.TestGroups
            .OrderBy(g => g.Order)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.NameEn,
                g.NameRu,
                g.Icon,
                g.Color,
                Tests = context.LabResults
                    .Where(r => r.UserId == uid && r.GroupId == g.Id)
                    .Select(r => r.TestName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList()
            })
            .ToListAsync();

        // Tests not assigned to any group
        var ungrouped = await context.LabResults
            .Where(r => r.UserId == uid && r.GroupId == null)
            .Select(r => r.TestName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        var result = groups.Where(g => g.Tests.Count > 0).ToList<object>();
        if (ungrouped.Count > 0)
            result.Add(new { Id = (int?)null, Name = "Інше", NameEn = "Other", NameRu = "Прочее", Icon = "other", Color = "#64748b", Tests = ungrouped });

        return Ok(result);
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
                IsAbnormal = x.IsAbnormal,
                GroupName = GroupDetectionService.DetectGroupName(x.TestName),
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

    [AllowAnonymous]
    [HttpGet("demo")]
    public IActionResult GetDemo()
    {
        var d1 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2025, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var d3 = new DateTime(2025, 11, 18, 0, 0, 0, DateTimeKind.Utc);

        var results = new object[]
        {
            // Jan 2025 — CBC + Lipids (elevated)
            new { id = 1,  testName = "Гемоглобін",           value = 138.0, unit = "г/л",      referenceRange = "130-175", isAbnormal = false, testDate = d1, patientName = "" },
            new { id = 2,  testName = "Лейкоцити",            value = 9.8,   unit = "10⁹/л",    referenceRange = "4.0-9.0", isAbnormal = true,  testDate = d1, patientName = "" },
            new { id = 3,  testName = "Еритроцити",           value = 4.8,   unit = "10¹²/л",   referenceRange = "4.0-5.5", isAbnormal = false, testDate = d1, patientName = "" },
            new { id = 4,  testName = "Тромбоцити",           value = 218.0, unit = "10⁹/л",    referenceRange = "150-400", isAbnormal = false, testDate = d1, patientName = "" },
            new { id = 5,  testName = "Холестерин загальний", value = 6.4,   unit = "ммоль/л",  referenceRange = "<5.2",    isAbnormal = true,  testDate = d1, patientName = "" },
            new { id = 6,  testName = "Холестерин ЛПНЩ",      value = 4.2,   unit = "ммоль/л",  referenceRange = "<3.0",    isAbnormal = true,  testDate = d1, patientName = "" },
            new { id = 7,  testName = "Холестерин ЛПВЩ",      value = 1.05,  unit = "ммоль/л",  referenceRange = ">1.0",    isAbnormal = false, testDate = d1, patientName = "" },
            new { id = 8,  testName = "Тригліцериди",         value = 2.6,   unit = "ммоль/л",  referenceRange = "<1.7",    isAbnormal = true,  testDate = d1, patientName = "" },

            // May 2025 — CBC + Biochemistry (improving)
            new { id = 9,  testName = "Гемоглобін",           value = 141.0, unit = "г/л",      referenceRange = "130-175", isAbnormal = false, testDate = d2, patientName = "" },
            new { id = 10, testName = "Лейкоцити",            value = 7.9,   unit = "10⁹/л",    referenceRange = "4.0-9.0", isAbnormal = false, testDate = d2, patientName = "" },
            new { id = 11, testName = "Еритроцити",           value = 5.0,   unit = "10¹²/л",   referenceRange = "4.0-5.5", isAbnormal = false, testDate = d2, patientName = "" },
            new { id = 12, testName = "Глюкоза",              value = 5.8,   unit = "ммоль/л",  referenceRange = "3.9-6.1", isAbnormal = false, testDate = d2, patientName = "" },
            new { id = 13, testName = "АЛТ",                  value = 52.0,  unit = "Од/л",     referenceRange = "<40",     isAbnormal = true,  testDate = d2, patientName = "" },
            new { id = 14, testName = "АСТ",                  value = 38.0,  unit = "Од/л",     referenceRange = "<40",     isAbnormal = false, testDate = d2, patientName = "" },
            new { id = 15, testName = "Холестерин загальний", value = 5.9,   unit = "ммоль/л",  referenceRange = "<5.2",    isAbnormal = true,  testDate = d2, patientName = "" },
            new { id = 16, testName = "Холестерин ЛПНЩ",      value = 3.5,   unit = "ммоль/л",  referenceRange = "<3.0",    isAbnormal = true,  testDate = d2, patientName = "" },
            new { id = 17, testName = "Тригліцериди",         value = 1.8,   unit = "ммоль/л",  referenceRange = "<1.7",    isAbnormal = true,  testDate = d2, patientName = "" },

            // Nov 2025 — all normalized
            new { id = 18, testName = "Гемоглобін",           value = 145.0, unit = "г/л",      referenceRange = "130-175", isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 19, testName = "Лейкоцити",            value = 6.2,   unit = "10⁹/л",    referenceRange = "4.0-9.0", isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 20, testName = "Еритроцити",           value = 5.1,   unit = "10¹²/л",   referenceRange = "4.0-5.5", isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 21, testName = "Тромбоцити",           value = 224.0, unit = "10⁹/л",    referenceRange = "150-400", isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 22, testName = "Глюкоза",              value = 5.3,   unit = "ммоль/л",  referenceRange = "3.9-6.1", isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 23, testName = "АЛТ",                  value = 34.0,  unit = "Од/л",     referenceRange = "<40",     isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 24, testName = "АСТ",                  value = 29.0,  unit = "Од/л",     referenceRange = "<40",     isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 25, testName = "Холестерин загальний", value = 4.9,   unit = "ммоль/л",  referenceRange = "<5.2",    isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 26, testName = "Холестерин ЛПНЩ",      value = 2.9,   unit = "ммоль/л",  referenceRange = "<3.0",    isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 27, testName = "Холестерин ЛПВЩ",      value = 1.3,   unit = "ммоль/л",  referenceRange = ">1.0",    isAbnormal = false, testDate = d3, patientName = "" },
            new { id = 28, testName = "Тригліцериди",         value = 1.4,   unit = "ммоль/л",  referenceRange = "<1.7",    isAbnormal = false, testDate = d3, patientName = "" },
        };
        return Ok(results);
    }

    [HttpPost("ai-summary")]
    public async Task<ActionResult> GetAiSummary(
        [FromBody] AiSummaryRequest request,
        CancellationToken cancellationToken)
    {
        if (labAiService is null)
            return StatusCode(503, "AI service not configured.");

        var creditError = await ConsumeAiCreditAsync(cancellationToken);
        if (creditError is not null) return creditError;

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
            var patient = new PatientContext { Sex = request.Sex, AgeYears = request.AgeYears, CycleDay = request.CycleDay };
            var summary = await labAiService.GetSummaryAsync(request.Date, items, request.Lang, patient.HasAny ? patient : null, cancellationToken);
            return Ok(new { summary });
        }
        catch (Exception)
        {
            return StatusCode(503, "Failed to generate AI summary.");
        }
    }

    [HttpPost("ai-explain")]
    public async Task<ActionResult> GetAiExplain(
        [FromBody] AiExplainRequest request,
        CancellationToken cancellationToken)
    {
        if (labAiService is null)
            return StatusCode(503, "AI service not configured.");

        var creditError = await ConsumeAiCreditAsync(cancellationToken);
        if (creditError is not null) return creditError;

        try
        {
            var patient = new PatientContext { Sex = request.Sex, AgeYears = request.AgeYears, CycleDay = request.CycleDay };
            var explanation = await labAiService.GetExplainAsync(
                request.TestName, request.Value, request.Unit, request.ReferenceRange, request.Lang,
                patient.HasAny ? patient : null, cancellationToken);
            return Ok(new { explanation });
        }
        catch (Exception)
        {
            return StatusCode(503, "Failed to generate explanation.");
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
                IsAbnormal = metric.IsAbnormal || LabResultHelper.IsOutOfRange(metric.Value, metric.ReferenceRange),
                UserId = userId,
                GroupId = GroupDetectionService.Detect(metric.TestName),
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