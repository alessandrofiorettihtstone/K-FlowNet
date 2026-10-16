using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KFlow.Class;
using KFlow.Models;
using KFlow.Services;

namespace KFlow.Pages.Mobile;

[AllowAnonymous]
public class DefaultModel : PageModel
{
    private readonly KFlowAuthService _authService;
    private readonly IConfiguration _configuration;

    public DefaultModel(KFlowAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [BindProperty]
    public string CID { get; set; } = string.Empty;

    public bool ShowLoginModal { get; set; }

    public string? LoginError { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Page();
        }

        var cookieName = _configuration["KFlow:LegacyLoginCookieName"] ?? "login";
        var legacyGuid = KFlowAuthService.ReadLegacyLoginCookie(Request.Cookies[cookieName]);
        if (legacyGuid is Guid guid)
        {
            var user = await _authService.FindByGuidAsync(guid);
            if (user is { Enabled: true })
            {
                await SignInUserAsync(user);
                return Redirect("/Mobile/Default.cshtml");
            }
        }

        ShowLoginModal = true;
        return Page();
    }

    public async Task<IActionResult> OnPostLoginAsync()
    {
        if (!Guid.TryParse(CID, out var userGuid))
        {
            LoginError = "Login fallito.";
            ShowLoginModal = true;
            return Page();
        }

        var user = await _authService.FindByGuidAsync(userGuid);
        if (user == null)
        {
            LoginError = "Login fallito.";
            ShowLoginModal = true;
            return Page();
        }

        if (!user.Enabled)
        {
            LoginError = "Utente non abilitato.";
            ShowLoginModal = true;
            return Page();
        }

        await SignInUserAsync(user);
        return Redirect("/Mobile/Default.cshtml");
    }

    private async Task SignInUserAsync(KFlow.Models.User user)
    {
        await HttpContext.SignOutAsync(AuthConfiguration.CookieScheme);
        await HttpContext.SignInAsync(
            AuthConfiguration.CookieScheme,
            _authService.CreatePrincipal(user, isMobileSession: true),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });

        var cookieName = _configuration["KFlow:LegacyLoginCookieName"] ?? "login";
        Response.Cookies.Append(
            cookieName,
            $"guid={Uri.EscapeDataString(user.Guid.ToString())}",
            new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(7),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });
    }
}
