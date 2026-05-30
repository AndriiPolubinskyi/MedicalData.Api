using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.JSInterop;

namespace MedicalData.Client.Services;

public class AuthService(IJSRuntime js)
{
    private const string Key = "auth_token";
    private ClaimsPrincipal? _cached;

    public async Task<string?> GetTokenAsync()
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", Key); }
        catch { return null; }
    }

    public async Task SetTokenAsync(string token)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", Key, token); } catch { }
        _cached = null;
    }

    public async Task ClearTokenAsync()
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", Key); } catch { }
        _cached = null;
    }

    public async Task<ClaimsPrincipal> GetUserAsync()
    {
        if (_cached != null) return _cached;

        var token = await GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return _cached = new ClaimsPrincipal(new ClaimsIdentity());

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            if (jwt.ValidTo < DateTime.UtcNow)
            {
                await ClearTokenAsync();
                return _cached = new ClaimsPrincipal(new ClaimsIdentity());
            }

            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            return _cached = new ClaimsPrincipal(identity);
        }
        catch
        {
            return _cached = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var user = await GetUserAsync();
        return user.Identity?.IsAuthenticated == true;
    }

    public void Invalidate() => _cached = null;
}
