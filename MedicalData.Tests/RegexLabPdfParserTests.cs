using Microsoft.Extensions.Logging.Abstractions;
using MedicalData.Api.Services;

namespace MedicalData.Tests;

/// <summary>
/// Tests for LabPdfAgentParser (regex-based fallback parser, no external dependencies).
/// </summary>
public class RegexLabPdfParserTests
{
    private static readonly LabPdfAgentParser Parser = new(NullLogger<LabPdfAgentParser>.Instance);

    public static TheoryData<string, DateTime, int> PdfDateCases => new()
    {
        { "Полубінський А. Я. 1979-04-03 - Біохімічні дослідження.pdf",         new DateTime(2026, 3, 19), 1 },
        { "Полубінський Андрій Якович - Імуноферментний аналіз - 01.03.2024.pdf", new DateTime(2024, 3, 1),  1 },
        { "Полубінський Андрій Якович - Біохімічне дослідження_- 31.05.2023.pdf", new DateTime(2023, 5, 31), 5 },
        { "Полубінський Андрій Якович - Загальний аналіз крові - 02.10.2023.pdf", new DateTime(2023, 10, 2), 10 },
        { "Полубінський Андрій Якович - Загальний аналіз крові - 07.08.2023.pdf", new DateTime(2023, 8, 7),  10 },
        { "Полубінський Андрій Якович - Загальний аналіз крові - 31.05.2023.pdf", new DateTime(2023, 5, 31), 10 },
    };

    [Theory]
    [MemberData(nameof(PdfDateCases))]
    public async Task ParseAsync_KnownPdf_ParsesDateCorrectly(
        string fileName, DateTime expectedDate, int minResultCount)
    {
        var pdfPath = Path.Combine(TestConfig.ExamplesPath, fileName);
        await using var stream = File.OpenRead(pdfPath);

        var result = await Parser.ParseAsync(stream);

        Assert.NotNull(result.Date);
        Assert.Equal(expectedDate, result.Date!.Value.Date);
        Assert.True(result.Results.Count >= minResultCount,
            $"Expected at least {minResultCount} results, got {result.Results.Count}. " +
            $"Results: {string.Join(", ", result.Results.Select(r => r.TestName))}");
    }

    [Fact]
    public async Task ParseAsync_BiochemOmega2023_DetectsAbnormalMarkers()
    {
        var pdfPath = Path.Combine(TestConfig.ExamplesPath,
            "Полубінський Андрій Якович - Біохімічне дослідження_- 31.05.2023.pdf");

        await using var stream = File.OpenRead(pdfPath);
        var result = await Parser.ParseAsync(stream);

        Assert.Contains(result.Results, r => r.IsAbnormal);

        // Коефіцієнт атерогенності = 5.36 !!!
        var atherogenic = result.Results.FirstOrDefault(r =>
            r.TestName.Contains("атерогенності", StringComparison.OrdinalIgnoreCase));
        if (atherogenic != null)
            Assert.True(atherogenic.IsAbnormal, "Коефіцієнт атерогенності 5.36 !!! should be abnormal");
    }

    [Fact]
    public async Task ParseAsync_AllExampleFiles_NeverThrows()
    {
        var failures = new List<string>();

        foreach (var pdf in Directory.GetFiles(TestConfig.ExamplesPath, "*.pdf"))
        {
            try
            {
                await using var stream = File.OpenRead(pdf);
                _ = await Parser.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(pdf)}: {ex.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ParseAsync_AllExampleFiles_ValuesInMedicalRange()
    {
        var outOfRange = new List<string>();

        foreach (var pdf in Directory.GetFiles(TestConfig.ExamplesPath, "*.pdf"))
        {
            await using var stream = File.OpenRead(pdf);
            var result = await Parser.ParseAsync(stream);

            foreach (var r in result.Results)
            {
                if (r.Value is < -1000 or > 10000)
                    outOfRange.Add($"{Path.GetFileName(pdf)}: {r.TestName} = {r.Value}");
            }
        }

        Assert.Empty(outOfRange);
    }
}
