namespace MedicalData.Api.Dto.Responses;

public class UploadPdfLabResultsResponse
{
    public DateTime Date { get; set; }
    public int SavedCount { get; set; }
    public List<ParsedLabMetricResponse> Results { get; set; } = new();
}

public class ParsedLabMetricResponse
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}
