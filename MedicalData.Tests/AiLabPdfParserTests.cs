using Microsoft.Extensions.Logging.Abstractions;
using MedicalData.Api.Services;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;

namespace MedicalData.Tests;

/// <summary>
/// Integration tests for AiLabPdfParser against real PDF examples.
/// Requires Anthropic API key — see TestInfrastructure.cs for configuration options.
/// </summary>
public class AiLabPdfParserTests
{
    private static AiLabPdfParser CreateParser()
    {
        var config = new ConfigurationBuilder()
            .AddConfiguration(TestConfig.Instance)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = TestConfig.AnthropicKey,
                ["Anthropic:Model"] = TestConfig.AnthropicModel
            })
            .Build();

        return new AiLabPdfParser(config, NullLogger<AiLabPdfParser>.Instance);
    }

    // ------------------------------------------------------------------
    // Per-file test cases:  (fileName, expectedDate, minResultCount,
    //                        spotCheckName, spotCheckValue, expectAbnormal)
    // ------------------------------------------------------------------
    public static TheoryData<string, DateTime, int, string, double, bool> PdfCases => new()
    {
        {
            "Полубінський А. Я. 1979-04-03 - Біохімічні дослідження.pdf",
            new DateTime(2026, 3, 19),
            6,
            "Холестерин (CHOL)", 5.65,
            false
        },
        {
            "Полубінський Андрій Якович - Імуноферментний аналіз - 01.03.2024.pdf",
            new DateTime(2024, 3, 1),
            1,
            "Тестостерон вільний (Free T)", 15.34,
            false
        },
        {
            "Полубінський Андрій Якович - Біохімічне дослідження_- 31.05.2023.pdf",
            new DateTime(2023, 5, 31),
            10,
            "Глюкоза (GLU)", 4.74,
            false
        },
        {
            "Полубінський Андрій Якович - Загальний аналіз крові - 02.10.2023.pdf",
            new DateTime(2023, 10, 2),
            15,
            "Гемоглобін (HGB)", 155.0,
            false
        },
        {
            "Полубінський Андрій Якович - Загальний аналіз крові - 07.08.2023.pdf",
            new DateTime(2023, 8, 7),
            15,
            "Гемоглобін (HGB)", 152.0,
            false
        },
        {
            "Полубінський Андрій Якович - Загальний аналіз крові - 31.05.2023.pdf",
            new DateTime(2023, 5, 31),
            15,
            "Гемоглобін (HGB)", 157.0,
            false
        },
    };

    [RequiresAnthropicTheory]
    [MemberData(nameof(PdfCases))]
    public async Task ParseAsync_KnownPdf_ReturnsExpectedDateAndValue(
        string fileName,
        DateTime expectedDate,
        int minResultCount,
        string spotCheckName,
        double spotCheckValue,
        bool _)
    {
        var parser = CreateParser();
        var pdfPath = Path.Combine(TestConfig.ExamplesPath, fileName);

        await using var stream = File.OpenRead(pdfPath);
        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result.Date);
        Assert.Equal(expectedDate, result.Date!.Value.Date);
        Assert.True(result.Results.Count >= minResultCount,
            $"Expected at least {minResultCount} results, got {result.Results.Count}. " +
            $"Results: {string.Join(", ", result.Results.Select(r => r.TestName))}");

        var spot = result.Results.FirstOrDefault(r =>
            string.Equals(r.TestName, spotCheckName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(spot);
        Assert.Equal(spotCheckValue, spot!.Value, precision: 2);
    }

    [RequiresAnthropicFact]
    public async Task ParseAsync_BiochemOmega2023_DetectsAbnormalValues()
    {
        var parser = CreateParser();
        var pdfPath = Path.Combine(TestConfig.ExamplesPath,
            "Полубінський Андрій Якович - Біохімічне дослідження_- 31.05.2023.pdf");

        await using var stream = File.OpenRead(pdfPath);
        var result = await parser.ParseAsync(stream);

        // Холестерин = 6.04 !!! in this PDF
        var cholesterol = result.Results.FirstOrDefault(r =>
            r.TestName.Contains("Холестерин", StringComparison.OrdinalIgnoreCase) &&
            !r.TestName.Contains("ЛПВЩ") && !r.TestName.Contains("ЛПНЩ") &&
            !r.TestName.Contains("ЛПДНЩ") && !r.TestName.Contains("не-"));
        Assert.NotNull(cholesterol);
        Assert.Equal(6.04, cholesterol!.Value, precision: 2);
        Assert.True(cholesterol.IsAbnormal, "Cholesterol 6.04 !!! should be marked abnormal");

        // Коефіцієнт атерогенності = 5.36 !!!
        var atherogenic = result.Results.FirstOrDefault(r =>
            r.TestName.Contains("атерогенності", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(atherogenic);
        Assert.True(atherogenic!.IsAbnormal, "Коефіцієнт атерогенності 5.36 !!! should be marked abnormal");
    }

    [RequiresAnthropicFact]
    public async Task ParseAsync_BloodCountOct2023_DetectsAbnormalLeukocyteFormula()
    {
        var parser = CreateParser();
        var pdfPath = Path.Combine(TestConfig.ExamplesPath,
            "Полубінський Андрій Якович - Загальний аналіз крові - 02.10.2023.pdf");

        await using var stream = File.OpenRead(pdfPath);
        var result = await parser.ParseAsync(stream);

        // Еозинофіли (%) = 6.0 !!!
        var eos = result.Results.FirstOrDefault(r =>
            r.TestName.Contains("Еозинофіл", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(eos);
        Assert.True(eos!.IsAbnormal, "Еозинофіли 6.0 !!! should be marked abnormal");
    }

    [RequiresAnthropicFact]
    public async Task ParseAsync_AllExampleFiles_ReturnNonEmptyResults()
    {
        var parser = CreateParser();
        var failures = new List<string>();

        foreach (var pdf in Directory.GetFiles(TestConfig.ExamplesPath, "*.pdf"))
        {
            await using var stream = File.OpenRead(pdf);
            var result = await parser.ParseAsync(stream);

            if (result.Results.Count == 0)
                failures.Add(Path.GetFileName(pdf) + " — no results");
        }

        Assert.Empty(failures);
    }

    [RequiresAnthropicFact]
    public async Task ParseAsync_AllExampleFiles_AllHaveParsedDate()
    {
        var parser = CreateParser();
        var failures = new List<string>();

        foreach (var pdf in Directory.GetFiles(TestConfig.ExamplesPath, "*.pdf"))
        {
            await using var stream = File.OpenRead(pdf);
            var result = await parser.ParseAsync(stream);

            if (result.Date is null)
                failures.Add(Path.GetFileName(pdf) + " — date is null");
        }

        Assert.Empty(failures);
    }
}
