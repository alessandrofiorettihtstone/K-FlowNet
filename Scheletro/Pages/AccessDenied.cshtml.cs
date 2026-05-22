using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;

namespace Scheletro.Pages;

[AllowAnonymous]
public class AccessDeniedModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Admin/Login");
        }

        TempData["AccessDeniedMessage"] = "Non hai i permessi per accedere alla pagina richiesta. Sei stato riportato nell'area consentita dal tuo ruolo.";
        return RedirectToPage(AuthConfiguration.GetLandingPage(User));
    }
}
