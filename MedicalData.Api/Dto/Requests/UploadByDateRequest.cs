namespace MedicalData.Api.Dto.Requests;

public class UploadByDateRequest
{
    public DateTime Date { get; set; }
    public Dictionary<string, double> Results { get; set; } = new();
}