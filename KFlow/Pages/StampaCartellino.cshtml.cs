using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Services;

namespace Scheletro.Pages;

[Authorize]
public class StampaCartellinoModel : PageModel
{
    private readonly PrinterService _printerService;

    public StampaCartellinoModel(PrinterService printerService)
    {
        _printerService = printerService;
    }

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public bool Success { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostPrintAsync(CancellationToken cancellationToken)
    {
        var result = await _printerService.PrintSampleCardAsync(cancellationToken);
        Success = result.Success;
        Message = result.Message;
        return Redirect("/StampaCartellino.cshtml");
    }
}
