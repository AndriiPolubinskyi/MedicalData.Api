namespace MedicalData.Api.Services;

public interface ILabAiService
{
    Task<string> GetSummaryAsync(DateTime date, IReadOnlyList<AiSummaryItem> results, string lang = "uk", PatientContext? patient = null, CancellationToken ct = default);
    Task<string> GetExplainAsync(string testName, double value, string unit, string referenceRange, string lang, PatientContext? patient = null, CancellationToken ct = default);
}

public class PatientContext
{
    public string? Sex { get; set; }
    public int? AgeYears { get; set; }
    public int? CycleDay { get; set; }

    public bool HasAny => !string.IsNullOrEmpty(Sex) || AgeYears.HasValue || CycleDay.HasValue;

    public string Describe(string lang) => lang switch
    {
        "en" => BuildEn(),
        "ru" => BuildRu(),
        _    => BuildUk(),
    };

    private string BuildEn()
    {
        var parts = new List<string>();
        if (Sex == "female") parts.Add("female"); else if (Sex == "male") parts.Add("male");
        if (AgeYears.HasValue) parts.Add($"{AgeYears} years old");
        if (CycleDay.HasValue && Sex == "female") parts.Add($"cycle day {CycleDay}");
        return string.Join(", ", parts);
    }

    private string BuildUk()
    {
        var parts = new List<string>();
        if (Sex == "female") parts.Add("жінка"); else if (Sex == "male") parts.Add("чоловік");
        if (AgeYears.HasValue) parts.Add($"{AgeYears} років");
        if (CycleDay.HasValue && Sex == "female") parts.Add($"день циклу {CycleDay}");
        return string.Join(", ", parts);
    }

    private string BuildRu()
    {
        var parts = new List<string>();
        if (Sex == "female") parts.Add("женщина"); else if (Sex == "male") parts.Add("мужчина");
        if (AgeYears.HasValue) parts.Add($"{AgeYears} лет");
        if (CycleDay.HasValue && Sex == "female") parts.Add($"день цикла {CycleDay}");
        return string.Join(", ", parts);
    }
}

public class AiSummaryItem
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}
