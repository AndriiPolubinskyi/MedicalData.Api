namespace MedicalData.Api.Dto.Requests;

public class AiSummaryRequest
{
    public DateTime Date { get; set; }
    public List<AiSummaryResultItem> Results { get; set; } = new();
}

public class AiSummaryResultItem
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}
