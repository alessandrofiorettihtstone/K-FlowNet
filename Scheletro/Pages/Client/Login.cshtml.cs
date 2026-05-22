using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;
using Scheletro.Models;
using Scheletro.Services;

namespace Scheletro.Pages.Client;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly KFlowAuthService _authService;
    private readonly LoggerDTO _logger;
    private readonly IConfiguration _configuration;

    public LoginModel(KFlowAuthService authService, LoggerDTO logger, IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [DataType(DataType.Password)]
    public string CID { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(AuthConfiguration.GetLandingPage(User));
        }

        var cookieName = _configuration["KFlow:LegacyLoginCookieName"] ?? "login";
        var legacyGuid = KFlowAuthService.ReadLegacyLoginCookie(Request.Cookies[cookieName]);
        if (legacyGuid is Guid guid)
        {
            var user = await _authService.FindByGuidAsync(guid);
            if (user is { Enabled: true })
            {
                await SignInUserAsync(user);
                return Redirect("/Client/KanbanBoard.cshtml");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Scheletro.Models.User? user = null;
        var username = Username.Trim();
        var password = Password;
        var cid = CID.Trim();

        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(cid))
        {
            ModelState.AddModelError(string.Empty, "Username o CID obbligatorio.");
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(cid))
        {
            if (!Guid.TryParse(cid, out var userGuid))
            {
                ModelState.AddModelError(nameof(CID), "CID non valido.");
                return Page();
            }

            user = await _authService.FindByGuidAsync(userGuid);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(nameof(Password), "Password obbligatoria.");
                return Page();
            }

            user = await _authService.FindByCredentialsAsync(username, password);
        }

        if (user == null)
        {
            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "Login",
                TypeFunction = "KO",
                TypeText = $"Login fallito per '{username}'."
            });
            ModelState.AddModelError(string.Empty, "Login fallito.");
            return Page();
        }

        if (!user.Enabled)
        {
            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "Login",
                TypeFunction = "KO",
                TypeText = $"Utente non attivo '{user.Username}'.",
                IDUser = user.IDUser.ToString()
            });
            ModelState.AddModelError(string.Empty, "Utente non attivo.");
            return Page();
        }

        await SignInUserAsync(user);
        _logger.Log(new LoggerDTO.logDTO
        {
            typeMessage = "Login",
            TypeFunction = "OK",
            TypeText = $"Login riuscito per '{user.Username}'.",
            IDUser = user.IDUser.ToString()
        });

        return Redirect("/Client/KanbanBoard.cshtml");
    }

    public IActionResult OnPostClear()
    {
        return Redirect("/Client/Login.cshtml");
    }

    private async Task SignInUserAsync(Scheletro.Models.User user)
    {
        await HttpContext.SignOutAsync(AuthConfiguration.CookieScheme);
        await HttpContext.SignInAsync(
            AuthConfiguration.CookieScheme,
            _authService.CreatePrincipal(user),
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
