using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MedicalData.Client.Services;

public class AuthStateProvider(AuthService auth) : AuthenticationStateProvider
{
    private static readonly AuthenticationState _anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private Task<AuthenticationState> _current = Task.FromResult(_anonymous);

    // Called synchronously by the Blazor auth system — must NOT do JS interop here.
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _current;

    // Call this once after the first render when JS interop is safe.
    public async Task InitializeAsync()
    {
        var user = await auth.GetUserAsync();
        _current = Task.FromResult(new AuthenticationState(user));
        NotifyAuthenticationStateChanged(_current);
    }

    public void NotifyChanged()
    {
        _current = auth.GetUserAsync()
            .ContinueWith(t => new AuthenticationState(
                t.IsCompletedSuccessfully ? t.Result : _anonymous.User));
        NotifyAuthenticationStateChanged(_current);
    }
}
