namespace MedicalData.Client.Models;

public class UploadPdfLabResultsResponse
{
    public DateTime Date { get; set; }
    public int SavedCount { get; set; }
    public List<ParsedLabMetric> Results { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public int ExistingMatchCount { get; set; }
    public string? ParseError { get; set; }
}

public class ParsedLabMetric
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
    public string? GroupName { get; set; }
}
