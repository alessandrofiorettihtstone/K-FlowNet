using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;

namespace Scheletro.Pages.Mobile;

public class LogoutModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LogoutModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(AuthConfiguration.CookieScheme);

        var cookieName = _configuration["KFlow:LegacyLoginCookieName"] ?? "login";
        Response.Cookies.Delete(cookieName);

        return Redirect("/Mobile/Default.cshtml");
    }
}
