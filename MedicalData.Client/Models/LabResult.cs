namespace MedicalData.Client.Models;

public class LabResult
{
    public int Id { get; set; }
    public string TestName { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
    public string ReferenceRange { get; set; } = "";
    public DateTime TestDate { get; set; }
    public string PatientName { get; set; } = "";
}