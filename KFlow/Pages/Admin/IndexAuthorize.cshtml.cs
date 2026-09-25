using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;

namespace Scheletro.Pages.Admin
{
    [Authorize]
    public class IndexAuthorizeModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Redirect(AuthConfiguration.GetLandingPage(User));
        }
    }
}
