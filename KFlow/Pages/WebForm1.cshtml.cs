using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Scheletro.Pages;

[Authorize]
public class WebForm1Model : PageModel
{
    public IActionResult OnGet()
    {
        return Redirect("/Client/KanbanBoard.cshtml");
    }
}
