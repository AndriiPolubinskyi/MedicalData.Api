namespace MedicalData.Api.Models;

public class LabResult
{
    public int Id { get; set; }
    public string TestName { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
    public string ReferenceRange { get; set; } = "";
    public DateTime TestDate { get; set; }
    public string PatientName { get; set; } = "";
    public bool IsAbnormal { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
}