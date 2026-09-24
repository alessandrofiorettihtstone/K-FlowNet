using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Scheletro.Class;
using Scheletro.Services;
using Scheletro.ViewModels;

namespace Scheletro.Pages.Client;

[Authorize]
public class KanbanBoardModel : PageModel
{
    private readonly KanbanService _kanbanService;
    private readonly LoggerDTO _logger;

    public KanbanBoardModel(KanbanService kanbanService, LoggerDTO logger)
    {
        _kanbanService = kanbanService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public KanbanFilters Filters { get; set; } = new();

    public KanbanBoardViewModel Board { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Board = await _kanbanService.GetBoardAsync(Filters);
    }

    public async Task<JsonResult> OnGetSuggerimentiRicercaAsync(string prefixText, string tipoRicerca)
    {
        if (string.IsNullOrWhiteSpace(prefixText) || prefixText.Trim().Length < 2)
        {
            return new JsonResult(Array.Empty<string>());
        }

        prefixText = prefixText.Trim();
        var searchType = tipoRicerca?.Trim().ToUpperInvariant();

        if (searchType == "CARD")
        {
            return new JsonResult(await _kanbanService.GetCardSuggestionsAsync(prefixText, 20));
        }

        if (searchType == "PARTNER")
        {
            return new JsonResult(await _kanbanService.GetPartnerSuggestionsAsync(prefixText, 20));
        }

        if (searchType == "PRODUCT")
        {
            return new JsonResult(await _kanbanService.GetProductSuggestionsAsync(prefixText, 20));
        }

        return new JsonResult(Array.Empty<string>());
    }

    public async Task<JsonResult> OnGetPartnersAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return new JsonResult(Array.Empty<string>());
        }

        return new JsonResult(await _kanbanService.GetPartnerSuggestionsAsync(term));
    }

    public async Task<JsonResult> OnGetProductsAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return new JsonResult(Array.Empty<string>());
        }

        return new JsonResult(await _kanbanService.GetProductSuggestionsAsync(term));
    }

    public async Task<JsonResult> OnGetCardsAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return new JsonResult(Array.Empty<string>());
        }

        return new JsonResult(await _kanbanService.GetCardSuggestionsAsync(term));
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(
        string? targetCardId,
        int? targetCombinationId,
        short newStatus,
        string? returnUrl)
    {
        return await ChangeStatusAsync(targetCardId, targetCombinationId, newStatus, returnUrl);
    }

    public async Task<IActionResult> OnPostDragDropStatusAsync(
        string targetCardId,
        short newStatus,
        string? returnUrl)
    {
        return await ChangeStatusAsync(targetCardId, null, newStatus, returnUrl, "DragDropStatus");
    }

    public async Task<IActionResult> OnPostUpdateCardAsync(
        string editCardId,
        int quantity,
        short status,
        string? returnUrl)
    {
        try
        {
            await _kanbanService.UpdateCardAsync(editCardId, quantity, status);
            SuccessMessage = $"Cartellino {editCardId} aggiornato.";

            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "Kanban",
                TypeFunction = "UpdateCard",
                TypeText = SuccessMessage,
                IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "Kanban",
                TypeFunction = "UpdateCardError",
                TypeText = ex.Message,
                IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return Redirect(Request.GetEncodedPathAndQuery());
    }

    private async Task<IActionResult> ChangeStatusAsync(
        string? targetCardId,
        int? targetCombinationId,
        short newStatus,
        string? returnUrl,
        string functionName = "ChangeStatus")
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(targetCardId))
            {
                await _kanbanService.ChangeCardStatusAsync(targetCardId, newStatus);
                SuccessMessage = $"Stato aggiornato per {targetCardId}.";
            }
            else if (targetCombinationId is int combinationId && combinationId > 0)
            {
                await _kanbanService.ChangeCombinationStatusAsync(combinationId, newStatus);
                SuccessMessage = $"Stato aggiornato per tutti i cartellini della combinazione {combinationId}.";
            }
            else
            {
                ErrorMessage = "Selezione cartellino non valida.";
            }

            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "Kanban",
                TypeFunction = functionName,
                TypeText = SuccessMessage ?? ErrorMessage ?? string.Empty,
                IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "Kanban",
                TypeFunction = $"{functionName}Error",
                TypeText = ex.Message,
                IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return Redirect(Request.GetEncodedPathAndQuery());
    }
}
