namespace MedicalData.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string GoogleId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? PictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Plan { get; set; } = "free"; // "free" | "unlimited"
    public int AiCreditsLeft { get; set; } = 3;

    public ICollection<LabResult> LabResults { get; set; } = [];
    public ICollection<ShareToken> ShareTokens { get; set; } = [];
}
