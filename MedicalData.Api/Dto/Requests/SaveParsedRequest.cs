namespace MedicalData.Api.Dto.Requests;

public class SaveParsedRequest
{
    public DateTime Date { get; set; }
    public List<SaveParsedMetric> Results { get; set; } = new();
}

public class SaveParsedMetric
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}
