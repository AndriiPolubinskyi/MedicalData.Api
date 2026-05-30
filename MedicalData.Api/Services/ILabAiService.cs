namespace MedicalData.Api.Services;

public interface ILabAiService
{
    Task<string> GetSummaryAsync(DateTime date, IReadOnlyList<AiSummaryItem> results, CancellationToken ct = default);
}

public class AiSummaryItem
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}
