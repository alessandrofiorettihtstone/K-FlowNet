using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;

namespace Scheletro.Pages.Admin
{
    [Authorize(Policy = AuthConfiguration.AdminPolicy)]
    public class IndexAdminModel : PageModel
    {
        public string Username { get; private set; } = string.Empty;
        public string Role { get; private set; } = string.Empty;
        public string? StatusMessage { get; private set; }

        public void OnGet()
        {
            Username = AuthConfiguration.GetDisplayName(User);
            Role = AuthConfiguration.GetRole(User);
            StatusMessage = TempData["AccessDeniedMessage"] as string;
        }
    }
}
