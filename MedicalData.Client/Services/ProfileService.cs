using System.Net.Http.Json;
using MedicalData.Client.Models;

namespace MedicalData.Client.Services;

public class ProfileService(HttpClient http)
{
    private UserProfile? _cached;

    public UserProfile? Current => _cached;

    public async Task LoadAsync()
    {
        try { _cached = await http.GetFromJsonAsync<UserProfile>("/api/profile"); }
        catch { _cached = null; }
    }

    public async Task SaveAsync(UserProfile profile)
    {
        await http.PutAsJsonAsync("/api/profile", profile);
        _cached = profile;
    }
}
