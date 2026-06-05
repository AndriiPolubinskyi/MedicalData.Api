namespace MedicalData.Api.Models;

public class TestGroupMapping
{
    public int Id { get; set; }
    public string Keyword { get; set; } = "";
    public int GroupId { get; set; }
    public TestGroup Group { get; set; } = null!;
}
