using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KFlow.Pages.Error;

[AllowAnonymous]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
