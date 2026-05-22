using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;

namespace Scheletro.Pages.Client;

[Authorize]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(AuthConfiguration.CookieScheme);
        Response.Cookies.Delete("login");
        return Redirect("/Client/Login.cshtml");
    }
}
