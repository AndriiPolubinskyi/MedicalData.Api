namespace MedicalData.Api.Dto.Requests;

public class AiExplainRequest
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public string Lang { get; set; } = "uk";
}
