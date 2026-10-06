using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Services;
using Scheletro.ViewModels;

namespace Scheletro.Pages.Mobile;

[Authorize]
public class LeggiCartellinoMainModel : PageModel
{
    private readonly KanbanService _kanbanService;

    public LeggiCartellinoMainModel(KanbanService kanbanService)
    {
        _kanbanService = kanbanService;
    }

    [BindProperty]
    public string Barcode { get; set; } = string.Empty;

    public CardDetailViewModel? Card { get; set; }

    public IReadOnlyList<StatusOptionViewModel> Statuses { get; set; } = Array.Empty<StatusOptionViewModel>();

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadStatusesAsync();
    }

    public async Task OnPostReadAsync()
    {
        await LoadStatusesAsync();

        if (string.IsNullOrWhiteSpace(Barcode))
        {
            ErrorMessage = "Nessun barcode inserito.";
            return;
        }

        Card = await _kanbanService.GetCardDetailAsync(Barcode.Trim());
        Barcode = string.Empty;
        ModelState.Remove(nameof(Barcode));

        if (Card == null)
        {
            ErrorMessage = "Nessun cartellino trovato.";
        }
    }

    public async Task OnPostChangeStatusAsync(string idCard, short newStatus)
    {
        await LoadStatusesAsync();
        var result = await _kanbanService.ChangeCardStatusFromMobileAsync(idCard, newStatus);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
        }
        else
        {
            SuccessMessage = result.Message;
        }

        Card = await _kanbanService.GetCardDetailAsync(idCard);
        Barcode = string.Empty;
    }

    private async Task LoadStatusesAsync()
    {
        var statuses = await _kanbanService.GetEnabledStatusesAsync();
        Statuses = statuses
            .Select(status => new StatusOptionViewModel
            {
                ID = status.ID,
                Description = status.Description,
                Color = KanbanService.NormalizeColor(status.Color),
                Selected = false
            })
            .ToList();
    }
}
