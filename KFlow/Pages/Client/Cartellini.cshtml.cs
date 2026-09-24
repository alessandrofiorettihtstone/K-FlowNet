using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheletro.Class;
using Scheletro.Models;
using Scheletro.Services;

namespace Scheletro.Pages.Client;

[Authorize]
public class CartelliniModel : PageModel
{
    private const char PartnerValueSeparator = '|';
    private const string CardPrefix = "CTL";
    private const int CardNumberWidth = 8;

    private readonly dbContext _db;
    private readonly LoggerDTO _logger;
    private readonly PrinterService _printerService;

    public CartelliniModel(dbContext db, LoggerDTO logger, PrinterService printerService)
    {
        _db = db;
        _logger = logger;
        _printerService = printerService;
    }

    [BindProperty(SupportsGet = true)]
    public CartelliniFilters Filters { get; set; } = new();

    [BindProperty]
    public CreateCardInput Input { get; set; } = new();

    [BindProperty]
    public EditCardInput EditInput { get; set; } = new();

    [BindProperty]
    public CreateCombinationInput CombinationInput { get; set; } = new();

    public IReadOnlyList<CartellinoRowViewModel> Rows { get; private set; } = Array.Empty<CartellinoRowViewModel>();

    public IReadOnlyList<SelectListItem> StatusFilterOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> StatusCreateOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> StatusEditOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> CombinationCreateOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> CombinationEditOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<LocationSelectOption> LocationOptions { get; private set; } = Array.Empty<LocationSelectOption>();

    public IReadOnlyList<SelectListItem> PrinterOptions { get; private set; } = Array.Empty<SelectListItem>();

    public bool ShowCreateCardModal { get; private set; }

    public bool ShowEditCardModal { get; private set; }

    public bool ShowCreateCombinationModal { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
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
            var values = await _db.Cards
                .AsNoTracking()
                .Where(x => x.IDCard.Contains(prefixText))
                .OrderBy(x => x.IDCard)
                .Select(x => x.IDCard)
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        if (searchType == "PARTNER")
        {
            var partnerQuery = BuildPartnerSuggestionQuery(prefixText);
            var values = await partnerQuery
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ID)
                .Select(x => $"{x.ID} - {x.Name}")
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        if (searchType == "CARD_PARTNER" || searchType == "EDIT_CARD_PARTNER" || searchType == "COMBINATION_PARTNER")
        {
            var partnerQuery = BuildPartnerSuggestionQuery(prefixText);
            var values = await partnerQuery
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ID)
                .Select(x => BuildPartnerLookupLabel(x.PartnerType, x.ID, x.Name))
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        if (searchType == "PRODUCT" || searchType == "CARD_PRODUCT" || searchType == "EDIT_CARD_PRODUCT" || searchType == "COMBINATION_PRODUCT")
        {
            var productQuery = BuildProductSuggestionQuery(prefixText);
            var values = await productQuery
                .OrderBy(x => x.ID)
                .Select(x => BuildProductLookupLabel(x.ID, x.Name, x.UMBase))
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        if (searchType == "CROSS")
        {
            var values = await _db.Combinations
                .AsNoTracking()
                .Where(x => x.IDCross.Contains(prefixText))
                .OrderBy(x => x.IDCross)
                .Select(x => x.IDCross)
                .Where(x => x != string.Empty)
                .Distinct()
                .Take(100)
                .ToListAsync();

            return new JsonResult(values);
        }

        return new JsonResult(Array.Empty<string>());
    }

    public async Task<JsonResult> OnGetCombinationDisponibiliAsync(
        string? partnerValue,
        string? productId,
        int? locationId,
        int? selectedCombinationId)
    {
        var options = await LoadFilteredCombinationOptionsAsync(
            partnerValue,
            productId,
            locationId ?? 0,
            selectedCombinationId ?? 0);

        return new JsonResult(options.Select(x => new
        {
            value = x.Value,
            text = x.Text,
            selected = x.Selected
        }).ToList());
    }

    public async Task<IActionResult> OnPostCreateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeCreateCardInput();

        Input.IDCard = await GenerateNextCardIdAsync();
        if (Input.Status == 0)
        {
            Input.Status = await GetDefaultStatusAsync();
        }

        await ValidateCreateCardInputAsync();

        if (!ModelState.IsValid)
        {
            ShowCreateCardModal = true;
            ErrorMessage = "Controlla i dati del cartellino.";
            await LoadPageDataAsync(preserveCreateInput: true);
            return Page();
        }

        try
        {
            var card = new Card
            {
                Guid = Guid.NewGuid(),
                IDCard = Input.IDCard,
                IDCombination = Input.IDCombination,
                Quantity = Input.Quantity,
                Status = Input.Status
            };

            _db.Cards.Add(card);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Cartellino {card.IDCard} creato correttamente.";
            Log("CreateCard", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowCreateCardModal = true;
            ErrorMessage = $"Errore durante la creazione del cartellino: {ex.Message}";
            Log("CreateCardError", ex.Message);
            await LoadPageDataAsync(preserveCreateInput: true);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostPrintCardAsync(
        string? idCard,
        string? printerValue,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idCard))
        {
            ErrorMessage = "Cartellino non valido per la stampa.";
            return RedirectAfterPost(returnUrl);
        }

        var printPayload = await LoadPrintPayloadAsync(idCard.Trim());
        if (printPayload is null)
        {
            ErrorMessage = $"Cartellino {idCard} non trovato.";
            return RedirectAfterPost(returnUrl);
        }

        var result = await _printerService.PrintCardAsync(printPayload, printerValue, cancellationToken);
        if (result.Success)
        {
            SuccessMessage = $"Stampa inviata per {printPayload.IDCard}.";
            Log("PrintCard", $"{SuccessMessage} {result.Message}");
        }
        else
        {
            ErrorMessage = result.Message;
            Log("PrintCardError", $"{printPayload.IDCard}: {result.Message}");
        }

        return RedirectAfterPost(returnUrl);
    }

    public async Task<IActionResult> OnGetPreviewCardAsync(
        string? idCard,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idCard))
        {
            return BadRequest("Cartellino non valido.");
        }

        var printPayload = await LoadPrintPayloadAsync(idCard.Trim());
        if (printPayload is null)
        {
            return NotFound("Cartellino non trovato.");
        }

        var result = await _printerService.RenderCardPreviewAsync(printPayload, cancellationToken);
        return result.Success && result.Content is not null
            ? File(result.Content, "image/png")
            : new ObjectResult(new { error = result.Message })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
    }

    public async Task<IActionResult> OnPostUpdateCardAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeEditCardInput();

        await ValidateEditCardInputAsync();

        if (!ModelState.IsValid)
        {
            ShowEditCardModal = true;
            ErrorMessage = "Controlla i dati del cartellino.";
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }

        try
        {
            var card = await _db.Cards.FirstOrDefaultAsync(x => x.IDCard == EditInput.IDCard);
            if (card is null)
            {
                ModelState.AddModelError("EditInput.IDCard", "Cartellino non trovato.");
                ShowEditCardModal = true;
                ErrorMessage = "Cartellino non trovato.";
                await LoadPageDataAsync(preserveEditInput: true);
                return Page();
            }

            card.IDCombination = EditInput.IDCombination;
            card.Quantity = EditInput.Quantity;
            card.Status = EditInput.Status;

            await _db.SaveChangesAsync();

            SuccessMessage = $"Cartellino {card.IDCard} aggiornato correttamente.";
            Log("UpdateCard", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowEditCardModal = true;
            ErrorMessage = $"Errore durante la modifica del cartellino: {ex.Message}";
            Log("UpdateCardError", ex.Message);
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateCombinationAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeCreateCombinationInput();

        var validation = await ValidateCreateCombinationInputAsync();

        if (!validation.IsValid || !ModelState.IsValid)
        {
            ShowCreateCombinationModal = true;
            ErrorMessage = "Controlla i dati della combination.";
            await LoadPageDataAsync(preserveCombinationInput: true);
            return Page();
        }

        try
        {
            var combination = new Combination
            {
                Guid = Guid.NewGuid(),
                PartnerType = validation.PartnerType,
                IDPartner = validation.PartnerId,
                LocationID = CombinationInput.LocationID,
                IDProduct = CombinationInput.IDProduct,
                IDCross = CombinationInput.IDCross ?? string.Empty,
                PDR = CombinationInput.PDR,
                MOQ = CombinationInput.MOQ
            };

            _db.Combinations.Add(combination);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Combination {combination.ID} creata correttamente.";
            Log("CreateCombination", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowCreateCombinationModal = true;
            ErrorMessage = $"Errore durante la creazione della combination: {ex.Message}";
            Log("CreateCombinationError", ex.Message);
            await LoadPageDataAsync(preserveCombinationInput: true);
            return Page();
        }
    }

    private async Task ValidateCreateCardInputAsync()
    {
        await ValidateCardFieldsAsync(
            prefix: "Input",
            idCard: Input.IDCard,
            partnerValue: Input.PartnerValue,
            productId: Input.IDProduct,
            locationId: Input.LocationID,
            idCombination: Input.IDCombination,
            quantity: Input.Quantity,
            status: Input.Status,
            requireExistingCard: false,
            requireUniqueCard: true);
    }

    private async Task ValidateEditCardInputAsync()
    {
        await ValidateCardFieldsAsync(
            prefix: "EditInput",
            idCard: EditInput.IDCard,
            partnerValue: EditInput.PartnerValue,
            productId: EditInput.IDProduct,
            locationId: EditInput.LocationID,
            idCombination: EditInput.IDCombination,
            quantity: EditInput.Quantity,
            status: EditInput.Status,
            requireExistingCard: true,
            requireUniqueCard: false);
    }

    private async Task ValidateCardFieldsAsync(
        string prefix,
        string idCard,
        string? partnerValue,
        string? productId,
        int locationId,
        int idCombination,
        int quantity,
        short status,
        bool requireExistingCard,
        bool requireUniqueCard)
    {
        if (string.IsNullOrWhiteSpace(idCard))
        {
            ModelState.AddModelError($"{prefix}.IDCard", "Il codice cartellino e obbligatorio.");
        }
        else if (idCard.Length > 50)
        {
            ModelState.AddModelError($"{prefix}.IDCard", "Il codice cartellino non puo superare 50 caratteri.");
        }
        else if (requireUniqueCard)
        {
            var alreadyExists = await _db.Cards
                .AsNoTracking()
                .AnyAsync(x => x.IDCard == idCard);

            if (alreadyExists)
            {
                ModelState.AddModelError($"{prefix}.IDCard", "Esiste gia un cartellino con questo codice.");
            }
        }
        else if (requireExistingCard)
        {
            var exists = await _db.Cards
                .AsNoTracking()
                .AnyAsync(x => x.IDCard == idCard);

            if (!exists)
            {
                ModelState.AddModelError($"{prefix}.IDCard", "Cartellino non trovato.");
            }
        }

        var normalizedPartnerValue = NormalizePartnerLookup(partnerValue);
        var hasValidPartner = TryParsePartnerValue(normalizedPartnerValue, out var partnerType, out var partnerId);
        if (!hasValidPartner)
        {
            ModelState.AddModelError($"{prefix}.PartnerValue", "Seleziona un cliente.");
        }
        else
        {
            var partnerExists = await _db.Partners
                .AsNoTracking()
                .AnyAsync(x => x.PartnerType == partnerType && x.ID == partnerId);

            if (!partnerExists)
            {
                hasValidPartner = false;
                ModelState.AddModelError($"{prefix}.PartnerValue", "Il cliente selezionato non esiste.");
            }
        }

        var normalizedProductId = NormalizeLookupFilter(productId);
        var hasValidProduct = !string.IsNullOrWhiteSpace(normalizedProductId);
        if (!hasValidProduct)
        {
            ModelState.AddModelError($"{prefix}.IDProduct", "Seleziona un articolo.");
        }
        else if (normalizedProductId.Length > 50)
        {
            hasValidProduct = false;
            ModelState.AddModelError($"{prefix}.IDProduct", "Il codice articolo non puo superare 50 caratteri.");
        }
        else
        {
            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.ID == normalizedProductId);

            if (!productExists)
            {
                hasValidProduct = false;
                ModelState.AddModelError($"{prefix}.IDProduct", "L'articolo selezionato non esiste.");
            }
        }

        var hasValidLocation = locationId >= 0;
        if (!hasValidLocation)
        {
            ModelState.AddModelError($"{prefix}.LocationID", "Seleziona una ubicazione valida.");
        }
        else if (locationId > 0 && hasValidPartner)
        {
            var locationMatchesPartner = await _db.Locations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.LocationID == locationId &&
                    x.PartnerType == partnerType &&
                    x.PartnerID == partnerId);

            if (!locationMatchesPartner)
            {
                hasValidLocation = false;
                ModelState.AddModelError($"{prefix}.LocationID", "L'ubicazione selezionata non appartiene al cliente scelto.");
            }
        }

        Combination? selectedCombination = null;
        if (idCombination <= 0)
        {
            ModelState.AddModelError($"{prefix}.IDCombination", "Seleziona una combination.");
        }
        else
        {
            selectedCombination = await _db.Combinations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == idCombination);

            if (selectedCombination is null)
            {
                ModelState.AddModelError($"{prefix}.IDCombination", "La combination selezionata non esiste.");
            }
        }

        if (selectedCombination is not null && hasValidPartner && hasValidProduct && hasValidLocation)
        {
            var matchesSelection =
                selectedCombination.PartnerType == partnerType &&
                selectedCombination.IDPartner == partnerId &&
                selectedCombination.IDProduct == normalizedProductId &&
                selectedCombination.LocationID == locationId;

            if (!matchesSelection)
            {
                ModelState.AddModelError($"{prefix}.IDCombination", "La combination selezionata non corrisponde a cliente, articolo e ubicazione.");
            }
        }

        if (quantity <= 0)
        {
            ModelState.AddModelError($"{prefix}.Quantity", "La quantita deve essere maggiore di zero.");
        }

        if (status <= 0)
        {
            ModelState.AddModelError($"{prefix}.Status", "Seleziona uno stato.");
        }
        else
        {
            var statusEnabled = await _db.Statuses
                .AsNoTracking()
                .AnyAsync(x => x.ID == status && x.Enabled);

            if (!statusEnabled)
            {
                ModelState.AddModelError($"{prefix}.Status", "Lo stato selezionato non esiste o non e abilitato.");
            }
        }
    }

    private async Task<(bool IsValid, string PartnerType, string PartnerId)> ValidateCreateCombinationInputAsync()
    {
        var isValid = true;
        var partnerType = string.Empty;
        var partnerId = string.Empty;

        if (!TryParsePartnerValue(CombinationInput.PartnerValue, out partnerType, out partnerId))
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.PartnerValue", "Seleziona un cliente.");
        }
        else
        {
            var partnerExists = await _db.Partners
                .AsNoTracking()
                .AnyAsync(x => x.PartnerType == partnerType && x.ID == partnerId);

            if (!partnerExists)
            {
                isValid = false;
                ModelState.AddModelError("CombinationInput.PartnerValue", "Il cliente selezionato non esiste.");
            }
        }

        if (string.IsNullOrWhiteSpace(CombinationInput.IDProduct))
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.IDProduct", "Seleziona un articolo.");
        }
        else if (CombinationInput.IDProduct.Length > 50)
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.IDProduct", "Il codice articolo non puo superare 50 caratteri.");
        }
        else
        {
            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.ID == CombinationInput.IDProduct);

            if (!productExists)
            {
                isValid = false;
                ModelState.AddModelError("CombinationInput.IDProduct", "L'articolo selezionato non esiste.");
            }
        }

        if (CombinationInput.LocationID < 0)
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.LocationID", "Seleziona una ubicazione valida.");
        }
        else if (CombinationInput.LocationID > 0 && !string.IsNullOrWhiteSpace(partnerType) && !string.IsNullOrWhiteSpace(partnerId))
        {
            var locationMatchesPartner = await _db.Locations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.LocationID == CombinationInput.LocationID &&
                    x.PartnerType == partnerType &&
                    x.PartnerID == partnerId);

            if (!locationMatchesPartner)
            {
                isValid = false;
                ModelState.AddModelError("CombinationInput.LocationID", "L'ubicazione selezionata non appartiene al cliente scelto.");
            }
        }

        if (!string.IsNullOrWhiteSpace(CombinationInput.IDCross) && CombinationInput.IDCross.Length > 50)
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.IDCross", "Il cross non puo superare 50 caratteri.");
        }

        if (CombinationInput.PDR < 0)
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.PDR", "PDR non puo essere negativo.");
        }

        if (CombinationInput.MOQ < 0)
        {
            isValid = false;
            ModelState.AddModelError("CombinationInput.MOQ", "MOQ non puo essere negativo.");
        }

        if (isValid)
        {
            var duplicateExists = await _db.Combinations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PartnerType == partnerType &&
                    x.IDPartner == partnerId &&
                    x.LocationID == CombinationInput.LocationID &&
                    x.IDProduct == CombinationInput.IDProduct &&
                    x.IDCross == (CombinationInput.IDCross ?? string.Empty));

            if (duplicateExists)
            {
                isValid = false;
                ModelState.AddModelError("CombinationInput.IDCross", "Esiste gia una combination con cliente, articolo, ubicazione e cross indicati.");
            }
        }

        return (isValid, partnerType, partnerId);
    }

    private IQueryable<Partner> BuildPartnerSuggestionQuery(string prefixText)
    {
        var byId = _db.Partners
            .AsNoTracking()
            .Where(x => x.ID.Contains(prefixText));

        var byName = _db.Partners.AsNoTracking().AsQueryable();
        foreach (var word in SplitSearchWords(prefixText))
        {
            var searchWord = word;
            byName = byName.Where(x => x.Name.Contains(searchWord));
        }

        return byId.Concat(byName);
    }

    private IQueryable<Product> BuildProductSuggestionQuery(string prefixText)
    {
        var byId = _db.Products
            .AsNoTracking()
            .Where(x => x.ID.Contains(prefixText));

        var byName = _db.Products.AsNoTracking().AsQueryable();
        foreach (var word in SplitSearchWords(prefixText))
        {
            var searchWord = word;
            byName = byName.Where(x => x.Name.Contains(searchWord));
        }

        return byId.Concat(byName);
    }

    private async Task LoadPageDataAsync(
        bool preserveCreateInput = false,
        bool preserveEditInput = false,
        bool preserveCombinationInput = false)
    {
        var enabledStatuses = await _db.Statuses
            .AsNoTracking()
            .Where(x => x.Enabled)
            .OrderBy(x => x.ID)
            .ToListAsync();

        var defaultStatus = GetDefaultStatus(enabledStatuses);

        if (!preserveCreateInput || Input.Status == 0)
        {
            Input.Status = defaultStatus;
        }

        //if (!preserveCreateInput || string.IsNullOrWhiteSpace(Input.IDCard))
        //{
        //    Input.IDCard = await GenerateNextCardIdAsync();
        //}

        if (!preserveEditInput || EditInput.Status == 0)
        {
            EditInput.Status = defaultStatus;
        }

        if (!preserveCombinationInput)
        {
            CombinationInput.PDR = 0;
            CombinationInput.MOQ = 0;
        }

        StatusFilterOptions = enabledStatuses
            .Select(status => new SelectListItem
            {
                Value = status.ID.ToString(),
                Text = status.Description,
                Selected = Filters.Status == status.ID
            })
            .ToList();

        StatusCreateOptions = BuildStatusOptions(enabledStatuses, Input.Status);
        StatusEditOptions = BuildStatusOptions(enabledStatuses, EditInput.Status);
        LocationOptions = await LoadLocationOptionsAsync();
        CombinationCreateOptions = await LoadFilteredCombinationOptionsAsync(Input.PartnerValue, Input.IDProduct, Input.LocationID, Input.IDCombination);
        CombinationEditOptions = await LoadFilteredCombinationOptionsAsync(EditInput.PartnerValue, EditInput.IDProduct, EditInput.LocationID, EditInput.IDCombination);
        PrinterOptions = (await _printerService.GetConfiguredPrintersAsync())
            .Select(printer => new SelectListItem
            {
                Value = printer.Value,
                Text = $"{printer.Name} - {printer.NetworkAddress}:{printer.Port}"
            })
            .ToList();
        Rows = await LoadRowsAsync();
    }

    private static IReadOnlyList<SelectListItem> BuildStatusOptions(IReadOnlyList<KFlowStatus> statuses, short selectedStatus)
    {
        return statuses
            .Select(status => new SelectListItem
            {
                Value = status.ID.ToString(),
                Text = $"{status.ID} - {status.Description}",
                Selected = selectedStatus == status.ID
            })
            .ToList();
    }

    private async Task<CartellinoPrintPayload?> LoadPrintPayloadAsync(string idCard)
    {
        var row = await _db.VwKanbanBoard
            .AsNoTracking()
            .Where(x => x.IDCard == idCard)
            .Select(x => new
            {
                x.IDCard,
                x.IDPartner,
                x.PartnerName,
                x.IDProduct,
                x.ProductName,
                x.Quantity,
                x.UMBase,
                x.LocationCode,
                x.LocationName,
                x.IDCross
            })
            .FirstOrDefaultAsync();

        return row is null
            ? null
            : new CartellinoPrintPayload(
                row.IDCard,
                row.IDPartner,
                row.PartnerName,
                row.IDProduct,
                row.ProductName,
                row.Quantity,
                row.UMBase,
                row.LocationCode,
                row.LocationName,
                row.IDCross);
    }

    private async Task<short> GetDefaultStatusAsync()
    {
        var enabledStatuses = await _db.Statuses
            .AsNoTracking()
            .Where(x => x.Enabled)
            .OrderBy(x => x.ID)
            .ToListAsync();

        return GetDefaultStatus(enabledStatuses);
    }

    private static short GetDefaultStatus(IReadOnlyList<KFlowStatus> enabledStatuses)
    {
        return enabledStatuses.FirstOrDefault(x => x.ID == 100)?.ID
            ?? enabledStatuses.FirstOrDefault()?.ID
            ?? default;
    }

    private async Task<IReadOnlyList<CartellinoRowViewModel>> LoadRowsAsync()
    {
        var query = _db.VwKanbanBoard.AsNoTracking().AsQueryable();

        var cardFilter = Filters.CardId?.Trim();
        if (!string.IsNullOrWhiteSpace(cardFilter))
        {
            query = query.Where(x => x.IDCard.Contains(cardFilter));
        }

        var partnerFilter = NormalizeLookupFilter(Filters.Partner);
        if (!string.IsNullOrWhiteSpace(partnerFilter))
        {
            query = query.Where(x => x.IDPartner.Contains(partnerFilter) || x.PartnerName.Contains(partnerFilter));
        }

        var productFilter = NormalizeLookupFilter(Filters.Product);
        if (!string.IsNullOrWhiteSpace(productFilter))
        {
            query = query.Where(x => x.IDProduct.Contains(productFilter) || x.ProductName.Contains(productFilter));
        }

        if (Filters.Status.HasValue)
        {
            query = query.Where(x => x.Status == Filters.Status.Value);
        }

        var rows = await (
            from board in query
            join combinationItem in _db.Combinations.AsNoTracking()
                on board.IDCombinazione equals combinationItem.ID into combinationJoin
            from combination in combinationJoin.DefaultIfEmpty()
            select new
            {
                board.IDCard,
                board.IDCombinazione,
                board.IDPartner,
                board.PartnerName,
                board.IDProduct,
                board.ProductName,
                board.Quantity,
                board.UMBase,
                board.Status,
                board.StatusDescription,
                board.Color,
                board.LocationCode,
                board.LocationName,
                board.IDCross,
                PartnerType = combination != null ? combination.PartnerType : string.Empty,
                LocationID = combination != null ? combination.LocationID : 0
            })
            .OrderBy(x => x.IDPartner)
            .ThenBy(x=> x.LocationCode)
            .ThenBy(x => x.IDProduct)
            .ThenBy(x => x.IDCard)
            .ToListAsync();

        return rows
            .Select(x => new CartellinoRowViewModel
            {
                IDCard = x.IDCard,
                IDCombinazione = x.IDCombinazione,
                PartnerType = x.PartnerType,
                PartnerValue = string.IsNullOrWhiteSpace(x.PartnerType) ? string.Empty : BuildPartnerValue(x.PartnerType, x.IDPartner),
                PartnerLookupLabel = string.IsNullOrWhiteSpace(x.PartnerType) ? x.IDPartner : BuildPartnerLookupLabel(x.PartnerType, x.IDPartner, x.PartnerName),
                IDPartner = x.IDPartner,
                PartnerName = x.PartnerName,
                IDProduct = x.IDProduct,
                ProductLookupLabel = BuildProductLookupLabel(x.IDProduct, x.ProductName, x.UMBase),
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                UMBase = x.UMBase,
                Status = x.Status,
                StatusDescription = x.StatusDescription,
                StatusColor = KanbanService.NormalizeColor(x.Color),
                LocationID = x.LocationID,
                LocationCode = x.LocationCode,
                LocationName = x.LocationName,
                IDCross = x.IDCross
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SelectListItem>> LoadFilteredCombinationOptionsAsync(
        string? partnerValue,
        string? productId,
        int locationId,
        int selectedCombinationId)
    {
        var normalizedPartnerValue = NormalizePartnerLookup(partnerValue);
        var normalizedProductId = NormalizeLookupFilter(productId);

        if (!TryParsePartnerValue(normalizedPartnerValue, out var partnerType, out var partnerId) ||
            string.IsNullOrWhiteSpace(normalizedProductId) ||
            locationId < 0)
        {
            return Array.Empty<SelectListItem>();
        }

        var combinations = await (
            from combination in _db.Combinations.AsNoTracking()
            where combination.PartnerType == partnerType &&
                  combination.IDPartner == partnerId &&
                  combination.IDProduct == normalizedProductId &&
                  combination.LocationID == locationId
            join partnerItem in _db.Partners.AsNoTracking()
                on new { combination.PartnerType, PartnerID = combination.IDPartner }
                equals new { partnerItem.PartnerType, PartnerID = partnerItem.ID } into partnerJoin
            from partner in partnerJoin.DefaultIfEmpty()
            join productItem in _db.Products.AsNoTracking()
                on combination.IDProduct equals productItem.ID into productJoin
            from product in productJoin.DefaultIfEmpty()
            join locationItem in _db.Locations.AsNoTracking()
                on combination.LocationID equals locationItem.LocationID into locationJoin
            from location in locationJoin.DefaultIfEmpty()
            select new
            {
                combination.ID,
                combination.IDPartner,
                PartnerName = partner != null ? partner.Name : null,
                combination.IDProduct,
                ProductName = product != null ? product.Name : null,
                LocationCode = location != null ? location.LocationCode : null,
                LocationName = location != null ? location.LocationName : null,
                combination.IDCross
            })
            .OrderBy(x => x.ID)
            .ToListAsync();

        return combinations
            .Select(x => new SelectListItem
            {
                Value = x.ID.ToString(),
                Text = BuildCombinationLabel(
                    x.ID,
                    x.IDPartner,
                    x.PartnerName,
                    x.IDProduct,
                    x.ProductName,
                    x.LocationCode,
                    x.LocationName,
                    x.IDCross),
                Selected = selectedCombinationId == x.ID
            })
            .ToList();
    }

    private async Task<IReadOnlyList<LocationSelectOption>> LoadLocationOptionsAsync()
    {
        var locations = await _db.Locations
            .AsNoTracking()
            .Select(x => new
            {
                x.LocationID,
                x.PartnerType,
                x.PartnerID,
                x.LocationCode,
                x.LocationName
            })
            .ToListAsync();

        var options = new List<LocationSelectOption>
        {
            new()
            {
                Value = 0,
                Text = "0 - Nessuna ubicazione",
                PartnerKey = string.Empty
            }
        };

        options.AddRange(locations
            .OrderBy(x => x.PartnerID)
            .ThenBy(x => x.LocationCode)
            .ThenBy(x => x.LocationID)
            .Select(x => new LocationSelectOption
            {
                Value = x.LocationID,
                Text = BuildLocationLabel(x.LocationID, x.PartnerType, x.PartnerID, x.LocationCode, x.LocationName),
                PartnerKey = BuildPartnerValue(x.PartnerType, x.PartnerID)
            }));

        return options;
    }

    private async Task<string> GenerateNextCardIdAsync()
    {
        var ids = await _db.Cards
            .AsNoTracking()
            .Where(x => x.IDCard.StartsWith(CardPrefix))
            .Select(x => x.IDCard)
            .ToListAsync();

        var maxNumber = 0;
        foreach (var id in ids)
        {
            if (TryGetCardNumber(id, out var number) && number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return $"{CardPrefix}{(maxNumber + 1).ToString($"D{CardNumberWidth}")}";
    }

    private static bool TryGetCardNumber(string? idCard, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(idCard) || !idCard.StartsWith(CardPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = idCard[CardPrefix.Length..];
        return suffix.Length > 0 && suffix.All(char.IsDigit) && int.TryParse(suffix, out number);
    }

    private static string BuildCombinationLabel(
        int id,
        string partnerId,
        string? partnerName,
        string productId,
        string? productName,
        string? locationCode,
        string? locationName,
        string? idCross)
    {
        var partnerLabel = string.IsNullOrWhiteSpace(partnerName)
            ? partnerId
            : $"{partnerId} - {partnerName}";

        var productLabel = string.IsNullOrWhiteSpace(productName)
            ? productId
            : $"{productId} - {productName}";

        var locationParts = new[] { locationCode, locationName }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var locationLabel = string.Join(" ", locationParts);

        var parts = new List<string>
        {
            id.ToString(),
            $"Cliente: {partnerLabel}",
            $"Articolo: {productLabel}"
        };

        if (!string.IsNullOrWhiteSpace(locationLabel))
        {
            parts.Add($"Ubicazione: {locationLabel}");
        }

        if (!string.IsNullOrWhiteSpace(idCross))
        {
            parts.Add($"Cross: {idCross}");
        }

        return string.Join(" - ", parts);
    }

    private static string BuildLocationLabel(
        int locationId,
        string partnerType,
        string partnerId,
        string locationCode,
        string locationName)
    {
        var locationParts = new[] { locationCode, locationName }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var locationLabel = string.Join(" ", locationParts);

        return string.IsNullOrWhiteSpace(locationLabel)
            ? $"{locationId} - {partnerType} {partnerId}"
            : $"{locationId} - {partnerType} {partnerId} - {locationLabel}";
    }

    private static string BuildPartnerLookupLabel(string partnerType, string partnerId, string? partnerName)
    {
        var value = BuildPartnerValue(partnerType, partnerId);
        return string.IsNullOrWhiteSpace(partnerName)
            ? $"{value} - {partnerType} - {partnerId}"
            : $"{value} - {partnerType} - {partnerId} - {partnerName}";
    }

    private static string BuildProductLookupLabel(string productId, string? productName, string? umBase)
    {
        var label = string.IsNullOrWhiteSpace(productName)
            ? productId
            : $"{productId} - {productName}";

        return string.IsNullOrWhiteSpace(umBase)
            ? label
            : $"{label} ({umBase})";
    }

    private static string NormalizeLookupFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
        return separatorIndex > 0 ? trimmed[..separatorIndex].Trim() : trimmed;
    }

    private void NormalizeCreateCardInput()
    {
        Input.IDCard = Input.IDCard?.Trim() ?? string.Empty;
        Input.PartnerValue = Input.PartnerValue?.Trim() ?? string.Empty;
        Input.IDProduct = Input.IDProduct?.Trim() ?? string.Empty;
    }

    private void NormalizeEditCardInput()
    {
        EditInput.IDCard = EditInput.IDCard?.Trim() ?? string.Empty;
        EditInput.PartnerValue = EditInput.PartnerValue?.Trim() ?? string.Empty;
        EditInput.IDProduct = EditInput.IDProduct?.Trim() ?? string.Empty;
    }

    private void NormalizeCreateCombinationInput()
    {
        CombinationInput.PartnerValue = NormalizePartnerLookup(CombinationInput.PartnerValue);
        CombinationInput.IDProduct = NormalizeLookupFilter(CombinationInput.IDProduct);
        CombinationInput.IDCross = CombinationInput.IDCross?.Trim() ?? string.Empty;
    }

    private static string BuildPartnerValue(string partnerType, string partnerId)
    {
        return $"{partnerType}{PartnerValueSeparator}{partnerId}";
    }

    private static string NormalizePartnerLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
        return separatorIndex > 0 ? trimmed[..separatorIndex].Trim() : trimmed;
    }

    private static string[] SplitSearchWords(string value)
    {
        return value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryParsePartnerValue(string? value, out string partnerType, out string partnerId)
    {
        partnerType = string.Empty;
        partnerId = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(PartnerValueSeparator, 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        partnerType = parts[0];
        partnerId = parts[1];
        return partnerType.Length == 1 && partnerId.Length <= 50;
    }

    private IActionResult RedirectAfterPost(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return LocalRedirect("/Client/Cartellini.cshtml");
    }

    private void Log(string functionName, string? text)
    {
        _logger.Log(new LoggerDTO.logDTO
        {
            typeMessage = "Cartellini",
            TypeFunction = functionName,
            TypeText = text ?? string.Empty,
            IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
        });
    }
}

public class CartelliniFilters
{
    public string? CardId { get; set; }

    public string? Partner { get; set; }

    public string? Product { get; set; }

    public short? Status { get; set; }
}

public class CreateCardInput
{
    [Required(ErrorMessage = "Il codice cartellino e obbligatorio.")]
    [StringLength(50, ErrorMessage = "Il codice cartellino non puo superare 50 caratteri.")]
    public string IDCard { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleziona un cliente.")]
    public string PartnerValue { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleziona un articolo.")]
    [StringLength(50, ErrorMessage = "Il codice articolo non puo superare 50 caratteri.")]
    public string IDProduct { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Seleziona una ubicazione valida.")]
    public int LocationID { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleziona una combination.")]
    public int IDCombination { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La quantita deve essere maggiore di zero.")]
    public int Quantity { get; set; } = 1;

    [Range(1, short.MaxValue, ErrorMessage = "Seleziona uno stato.")]
    public short Status { get; set; }
}

public class EditCardInput
{
    [Required(ErrorMessage = "Il codice cartellino e obbligatorio.")]
    [StringLength(50, ErrorMessage = "Il codice cartellino non puo superare 50 caratteri.")]
    public string IDCard { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleziona un cliente.")]
    public string PartnerValue { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleziona un articolo.")]
    [StringLength(50, ErrorMessage = "Il codice articolo non puo superare 50 caratteri.")]
    public string IDProduct { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Seleziona una ubicazione valida.")]
    public int LocationID { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleziona una combination.")]
    public int IDCombination { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La quantita deve essere maggiore di zero.")]
    public int Quantity { get; set; } = 1;

    [Range(1, short.MaxValue, ErrorMessage = "Seleziona uno stato.")]
    public short Status { get; set; }
}

public class CreateCombinationInput
{
    [Required(ErrorMessage = "Seleziona un cliente.")]
    public string PartnerValue { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleziona un articolo.")]
    [StringLength(50, ErrorMessage = "Il codice articolo non puo superare 50 caratteri.")]
    public string IDProduct { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Seleziona una ubicazione valida.")]
    public int LocationID { get; set; }

    [StringLength(50, ErrorMessage = "Il cross non puo superare 50 caratteri.")]
    public string? IDCross { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "PDR non puo essere negativo.")]
    public int PDR { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "MOQ non puo essere negativo.")]
    public int MOQ { get; set; }
}

public class CartellinoRowViewModel
{
    public string IDCard { get; set; } = string.Empty;

    public int IDCombinazione { get; set; }

    public string PartnerType { get; set; } = string.Empty;

    public string PartnerValue { get; set; } = string.Empty;

    public string PartnerLookupLabel { get; set; } = string.Empty;

    public string IDPartner { get; set; } = string.Empty;

    public string PartnerName { get; set; } = string.Empty;

    public string IDProduct { get; set; } = string.Empty;

    public string ProductLookupLabel { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string UMBase { get; set; } = string.Empty;

    public short Status { get; set; }

    public string StatusDescription { get; set; } = string.Empty;

    public string StatusColor { get; set; } = string.Empty;

    public int LocationID { get; set; }

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }

    public string IDCross { get; set; } = string.Empty;
}

public class LocationSelectOption
{
    public int Value { get; set; }

    public string Text { get; set; } = string.Empty;

    public string PartnerKey { get; set; } = string.Empty;
}
