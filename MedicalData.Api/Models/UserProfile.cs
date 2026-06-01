namespace MedicalData.Api.Models;

public class UserProfile
{
    public Guid UserId { get; set; }
    public string Sex { get; set; } = "";          // "male" | "female"
    public DateTime? BirthDate { get; set; }
    public int? CycleDay { get; set; }             // 1–28, female only

    public User User { get; set; } = null!;
}
