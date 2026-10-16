using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KFlow.Pages;

[AllowAnonymous]
public class BarcodeScannerModel : PageModel
{
    public void OnGet()
    {
    }
}
