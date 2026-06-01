namespace MedicalData.Api.Dto.Requests;

public class AiSummaryRequest
{
    public DateTime Date { get; set; }
    public List<AiSummaryResultItem> Results { get; set; } = new();
    public string Lang { get; set; } = "uk";
    public string? Sex { get; set; }
    public int? AgeYears { get; set; }
    public int? CycleDay { get; set; }
}

public class AiSummaryResultItem
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}
