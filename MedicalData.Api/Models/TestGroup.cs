namespace MedicalData.Api.Models;

public class TestGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string NameRu { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    public int Order { get; set; }
    public ICollection<TestGroupMapping> Mappings { get; set; } = [];
    public ICollection<LabResult> LabResults { get; set; } = [];
}
