using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Scheletro.Pages;

[AllowAnonymous]
public class BarcodeScannerModel : PageModel
{
    public void OnGet()
    {
    }
}
