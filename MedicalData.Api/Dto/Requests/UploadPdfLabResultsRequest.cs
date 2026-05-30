namespace MedicalData.Api.Dto.Requests;

public class UploadPdfLabResultsRequest
{
    public IFormFile? File { get; set; }
    public DateTime? Date { get; set; }
    public bool Persist { get; set; } = true;
}
