namespace MedicalData.Client.Models;

public class UserProfile
{
    public string Sex { get; set; } = "";
    public string? BirthDate { get; set; }
    public int? CycleDay { get; set; }

    public int? AgeYears => DateTime.TryParse(BirthDate, out var bd)
        ? (int)((DateTime.Today - bd).TotalDays / 365.25)
        : null;
}
