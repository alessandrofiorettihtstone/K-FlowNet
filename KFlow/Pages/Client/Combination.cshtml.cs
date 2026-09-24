using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Scheletro.Class;
using Scheletro.Models;

namespace Scheletro.Pages.Client;

[Authorize]
public class CombinationModel : PageModel
{
    private const char PartnerValueSeparator = '|';

    private readonly dbContext _db;
    private readonly LoggerDTO _logger;

    public CombinationModel(dbContext db, LoggerDTO logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public CombinationFilters Filters { get; set; } = new();

    [BindProperty]
    public ManageCombinationInput Input { get; set; } = new();

    [BindProperty]
    public EditManageCombinationInput EditInput { get; set; } = new();

    public IReadOnlyList<CombinationRowViewModel> Rows { get; private set; } = Array.Empty<CombinationRowViewModel>();

    public IReadOnlyList<LocationSelectOption> LocationOptions { get; private set; } = Array.Empty<LocationSelectOption>();

    public bool ShowCreateModal { get; private set; }

    public bool ShowEditModal { get; private set; }

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

        if (searchType == "PARTNER")
        {
            var values = await BuildPartnerSuggestionQuery(prefixText)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ID)
                .Select(x => BuildPartnerLookupLabel(x.PartnerType, x.ID, x.Name))
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        if (searchType == "PRODUCT")
        {
            var values = await BuildProductSuggestionQuery(prefixText)
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
                .Take(50)
                .ToListAsync();

            return new JsonResult(values);
        }

        return new JsonResult(Array.Empty<string>());
    }

    public async Task<IActionResult> OnPostCreateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeCreateInput();
        var validation = await ValidateCombinationInputAsync(
            "Input",
            Input.PartnerValue,
            Input.IDProduct,
            Input.LocationID,
            Input.IDCross,
            Input.PDR,
            Input.MOQ);

        if (!validation.IsValid || !ModelState.IsValid)
        {
            ShowCreateModal = true;
            ErrorMessage = "Controlla i dati della combination.";
            await LoadPageDataAsync();
            return Page();
        }

        try
        {
            var combination = new Combination
            {
                Guid = Guid.NewGuid(),
                PartnerType = validation.PartnerType,
                IDPartner = validation.PartnerId,
                LocationID = Input.LocationID,
                IDProduct = Input.IDProduct,
                IDCross = Input.IDCross ?? string.Empty,
                PDR = Input.PDR,
                MOQ = Input.MOQ
            };

            _db.Combinations.Add(combination);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Combination {combination.ID} creata correttamente.";
            Log("CreateCombination", SuccessMessage);
            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowCreateModal = true;
            ErrorMessage = $"Errore durante la creazione della combination: {ex.Message}";
            Log("CreateCombinationError", ex.Message);
            await LoadPageDataAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeEditInput();

        if (EditInput.ID <= 0)
        {
            ModelState.AddModelError("EditInput.ID", "Combination non valida.");
        }

        var validation = await ValidateCombinationInputAsync(
            "EditInput",
            EditInput.PartnerValue,
            EditInput.IDProduct,
            EditInput.LocationID,
            EditInput.IDCross,
            EditInput.PDR,
            EditInput.MOQ,
            EditInput.ID);

        if (!validation.IsValid || !ModelState.IsValid)
        {
            ShowEditModal = true;
            ErrorMessage = "Controlla i dati della combination.";
            await LoadPageDataAsync();
            return Page();
        }

        try
        {
            var combination = await _db.Combinations.FirstOrDefaultAsync(x => x.ID == EditInput.ID);
            if (combination is null)
            {
                ShowEditModal = true;
                ErrorMessage = "Combination non trovata.";
                ModelState.AddModelError("EditInput.ID", "Combination non trovata.");
                await LoadPageDataAsync();
                return Page();
            }

            combination.PartnerType = validation.PartnerType;
            combination.IDPartner = validation.PartnerId;
            combination.LocationID = EditInput.LocationID;
            combination.IDProduct = EditInput.IDProduct;
            combination.IDCross = EditInput.IDCross ?? string.Empty;
            combination.PDR = EditInput.PDR;
            combination.MOQ = EditInput.MOQ;

            await _db.SaveChangesAsync();

            SuccessMessage = $"Combination {combination.ID} aggiornata correttamente.";
            Log("UpdateCombination", SuccessMessage);
            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowEditModal = true;
            ErrorMessage = $"Errore durante la modifica della combination: {ex.Message}";
            Log("UpdateCombinationError", ex.Message);
            await LoadPageDataAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? returnUrl)
    {
        try
        {
            var combination = await _db.Combinations.FirstOrDefaultAsync(x => x.ID == id);
            if (combination is null)
            {
                ErrorMessage = "Combination non trovata.";
                return RedirectAfterPost(returnUrl);
            }

            _db.Combinations.Remove(combination);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Combination {id} eliminata correttamente.";
            Log("DeleteCombination", SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Errore durante l'eliminazione della combination: {ex.Message}";
            Log("DeleteCombinationError", ex.Message);
        }

        return RedirectAfterPost(returnUrl);
    }

    private async Task<(bool IsValid, string PartnerType, string PartnerId)> ValidateCombinationInputAsync(
        string prefix,
        string partnerValue,
        string productId,
        int locationId,
        string? idCross,
        int pdr,
        int moq,
        int? currentId = null)
    {
        var isValid = true;
        var partnerType = string.Empty;
        var partnerId = string.Empty;
        var normalizedCross = idCross ?? string.Empty;

        if (!TryParsePartnerValue(partnerValue, out partnerType, out partnerId))
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.PartnerValue", "Seleziona un cliente.");
        }
        else
        {
            var partnerExists = await _db.Partners
                .AsNoTracking()
                .AnyAsync(x => x.PartnerType == partnerType && x.ID == partnerId);

            if (!partnerExists)
            {
                isValid = false;
                ModelState.AddModelError($"{prefix}.PartnerValue", "Il cliente selezionato non esiste.");
            }
        }

        if (string.IsNullOrWhiteSpace(productId))
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.IDProduct", "Seleziona un articolo.");
        }
        else if (productId.Length > 50)
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.IDProduct", "Il codice articolo non puo superare 50 caratteri.");
        }
        else
        {
            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.ID == productId);

            if (!productExists)
            {
                isValid = false;
                ModelState.AddModelError($"{prefix}.IDProduct", "L'articolo selezionato non esiste.");
            }
        }

        if (locationId < 0)
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.LocationID", "Seleziona una ubicazione valida.");
        }
        else if (locationId > 0 && !string.IsNullOrWhiteSpace(partnerType) && !string.IsNullOrWhiteSpace(partnerId))
        {
            var locationMatchesPartner = await _db.Locations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.LocationID == locationId &&
                    x.PartnerType == partnerType &&
                    x.PartnerID == partnerId);

            if (!locationMatchesPartner)
            {
                isValid = false;
                ModelState.AddModelError($"{prefix}.LocationID", "L'ubicazione selezionata non appartiene al cliente scelto.");
            }
        }

        if (normalizedCross.Length > 50)
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.IDCross", "Il cross non puo superare 50 caratteri.");
        }

        if (pdr < 0)
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.PDR", "PDR non puo essere negativo.");
        }

        if (moq < 0)
        {
            isValid = false;
            ModelState.AddModelError($"{prefix}.MOQ", "MOQ non puo essere negativo.");
        }

        if (isValid)
        {
            var duplicateExists = await _db.Combinations
                .AsNoTracking()
                .AnyAsync(x =>
                    (!currentId.HasValue || x.ID != currentId.Value) &&
                    x.PartnerType == partnerType &&
                    x.IDPartner == partnerId &&
                    x.LocationID == locationId &&
                    x.IDProduct == productId &&
                    x.IDCross == normalizedCross);

            if (duplicateExists)
            {
                isValid = false;
                ModelState.AddModelError($"{prefix}.IDCross", "Esiste gia una combination con cliente, articolo, ubicazione e cross indicati.");
            }
        }

        return (isValid, partnerType, partnerId);
    }

    private async Task LoadPageDataAsync()
    {
        LocationOptions = await LoadLocationOptionsAsync();
        Rows = await LoadRowsAsync();
    }

    private async Task<IReadOnlyList<CombinationRowViewModel>> LoadRowsAsync()
    {
        var query = _db.Combinations.AsNoTracking().AsQueryable();

        var partnerFilter = NormalizeLookupFilter(Filters.Partner);
        if (!string.IsNullOrWhiteSpace(partnerFilter))
        {
            if (TryParsePartnerValue(partnerFilter, out var partnerType, out var partnerId))
            {
                query = query.Where(x => x.PartnerType == partnerType && x.IDPartner == partnerId);
            }
            else
            {
                query = query.Where(x => x.IDPartner.Contains(partnerFilter));
            }
        }

        var productFilter = NormalizeLookupFilter(Filters.Product);
        if (!string.IsNullOrWhiteSpace(productFilter))
        {
            query = query.Where(x => x.IDProduct.Contains(productFilter));
        }

        if (!string.IsNullOrWhiteSpace(Filters.Cross))
        {
            var cross = Filters.Cross.Trim();
            query = query.Where(x => x.IDCross.Contains(cross));
        }

        var rows = await (
            from combination in query
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
                combination.PartnerType,
                combination.IDPartner,
                PartnerName = partner != null ? partner.Name : string.Empty,
                combination.LocationID,
                LocationCode = location != null ? location.LocationCode : string.Empty,
                LocationName = location != null ? location.LocationName : string.Empty,
                combination.IDProduct,
                ProductName = product != null ? product.Name : string.Empty,
                UMBase = product != null ? product.UMBase : string.Empty,
                combination.IDCross,
                combination.PDR,
                combination.MOQ
            })
            .OrderBy(x => x.ID)
            .Take(500)
            .ToListAsync();

        return rows
            .Select(x => new CombinationRowViewModel
            {
                ID = x.ID,
                PartnerValue = BuildPartnerValue(x.PartnerType, x.IDPartner),
                PartnerLabel = BuildPartnerLookupLabel(x.PartnerType, x.IDPartner, x.PartnerName),
                PartnerDisplay = string.IsNullOrWhiteSpace(x.PartnerName) ? x.IDPartner : $"{x.IDPartner} - {x.PartnerName}",
                LocationID = x.LocationID,
                LocationDisplay = BuildLocationDisplay(x.LocationID, x.LocationCode, x.LocationName),
                IDProduct = x.IDProduct,
                ProductLookupLabel = BuildProductLookupLabel(x.IDProduct, x.ProductName, x.UMBase),
                ProductDisplay = string.IsNullOrWhiteSpace(x.ProductName) ? x.IDProduct : $"{x.IDProduct} - {x.ProductName}",
                IDCross = x.IDCross,
                PDR = x.PDR,
                MOQ = x.MOQ
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

    private void NormalizeCreateInput()
    {
        Input.PartnerValue = NormalizePartnerLookup(Input.PartnerValue);
        Input.IDProduct = NormalizeLookupFilter(Input.IDProduct);
        Input.IDCross = Input.IDCross?.Trim() ?? string.Empty;
    }

    private void NormalizeEditInput()
    {
        EditInput.PartnerValue = NormalizePartnerLookup(EditInput.PartnerValue);
        EditInput.IDProduct = NormalizeLookupFilter(EditInput.IDProduct);
        EditInput.IDCross = EditInput.IDCross?.Trim() ?? string.Empty;
    }

    private IActionResult RedirectAfterPost(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return LocalRedirect("/Client/Combination.cshtml");
    }

    private void Log(string functionName, string? text)
    {
        _logger.Log(new LoggerDTO.logDTO
        {
            typeMessage = "Combination",
            TypeFunction = functionName,
            TypeText = text ?? string.Empty,
            IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
        });
    }

    private static string BuildPartnerValue(string partnerType, string partnerId)
    {
        return $"{partnerType}{PartnerValueSeparator}{partnerId}";
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

    private static string BuildLocationDisplay(int locationId, string? locationCode, string? locationName)
    {
        if (locationId == 0)
        {
            return "Nessuna ubicazione";
        }

        var locationParts = new[] { locationCode, locationName }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var locationLabel = string.Join(" ", locationParts);

        return string.IsNullOrWhiteSpace(locationLabel)
            ? locationId.ToString()
            : $"{locationId} - {locationLabel}";
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

    private static string[] SplitSearchWords(string value)
    {
        return value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

public class CombinationFilters
{
    public string? Partner { get; set; }

    public string? Product { get; set; }

    public string? Cross { get; set; }
}

public class ManageCombinationInput
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

public class EditManageCombinationInput : ManageCombinationInput
{
    public int ID { get; set; }
}

public class CombinationRowViewModel
{
    public int ID { get; set; }

    public string PartnerValue { get; set; } = string.Empty;

    public string PartnerLabel { get; set; } = string.Empty;

    public string PartnerDisplay { get; set; } = string.Empty;

    public int LocationID { get; set; }

    public string LocationDisplay { get; set; } = string.Empty;

    public string IDProduct { get; set; } = string.Empty;

    public string ProductLookupLabel { get; set; } = string.Empty;

    public string ProductDisplay { get; set; } = string.Empty;

    public string IDCross { get; set; } = string.Empty;

    public int PDR { get; set; }

    public int MOQ { get; set; }
}
