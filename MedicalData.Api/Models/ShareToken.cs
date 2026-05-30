namespace MedicalData.Api.Models;

public class ShareToken
{
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>"all" | "latest"</summary>
    public string Scope { get; set; } = "all";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
