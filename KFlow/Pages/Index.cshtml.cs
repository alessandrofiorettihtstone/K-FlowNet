using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KFlow.Class;

namespace KFlow.Pages;

[AllowAnonymous]
public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(AuthConfiguration.GetLandingPage(User));
        }

        return Redirect("/Client/Login.cshtml");
    }
}
