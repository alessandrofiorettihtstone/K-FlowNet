using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;

namespace Scheletro.Pages.Admin;

[Authorize]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(AuthConfiguration.CookieScheme);
        HttpContext.Session.Remove("KanbanBoard.SelectedStatusIds");
        Response.Cookies.Delete("login");
        Response.Cookies.Delete("KFlow.KanbanStatusIds", new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        });
        return Redirect("/Client/Login.cshtml");
    }
}
