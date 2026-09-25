using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheletro.Class;
using Scheletro.Models;

namespace Scheletro.Pages.Client;

[Authorize]
public class ClientiModel : PageModel
{
    private readonly dbContext _db;
    private readonly LoggerDTO _logger;

    public ClientiModel(dbContext db, LoggerDTO logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public ClientiFilters Filters { get; set; } = new();

    [BindProperty]
    public CreateClienteInput Input { get; set; } = new();

    [BindProperty]
    public EditClienteInput EditInput { get; set; } = new();

    public IReadOnlyList<ClienteRowViewModel> Rows { get; private set; } = Array.Empty<ClienteRowViewModel>();

    public IReadOnlyList<SelectListItem> PartnerTypeOptions { get; private set; } = Array.Empty<SelectListItem>();

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
        if (!string.Equals(tipoRicerca?.Trim(), "CLIENTE", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(Array.Empty<string>());
        }

        var values = await BuildPartnerSuggestionQuery(prefixText)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.ID)
            .Select(x => $"{x.ID} - {x.Name}")
            .Distinct()
            .Take(20)
            .ToListAsync();

        return new JsonResult(values);
    }

    public async Task<IActionResult> OnPostCreateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeCreateInput();

        await ValidateCreateInputAsync();

        if (!ModelState.IsValid)
        {
            ShowCreateModal = true;
            ErrorMessage = "Controlla i dati del cliente.";
            await LoadPageDataAsync(preserveCreateInput: true);
            return Page();
        }

        try
        {
            var partner = new Partner
            {
                Guid = Guid.NewGuid(),
                PartnerType = Input.PartnerType,
                ID = Input.ID,
                Name = Input.Name,
                Sector = Input.Sector
            };

            _db.Partners.Add(partner);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Cliente {partner.ID} creato correttamente.";
            Log("CreateCliente", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowCreateModal = true;
            ErrorMessage = $"Errore durante la creazione del cliente: {ex.Message}";
            Log("CreateClienteError", ex.Message);
            await LoadPageDataAsync(preserveCreateInput: true);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeEditInput();

        await ValidateEditInputAsync();

        if (!ModelState.IsValid)
        {
            ShowEditModal = true;
            ErrorMessage = "Controlla i dati del cliente.";
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }

        try
        {
            var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Guid == EditInput.Guid);
            if (partner is null)
            {
                ShowEditModal = true;
                ErrorMessage = "Cliente non trovato.";
                ModelState.AddModelError("EditInput.Guid", "Cliente non trovato.");
                await LoadPageDataAsync(preserveEditInput: true);
                return Page();
            }

            partner.Name = EditInput.Name;
            partner.Sector = EditInput.Sector;

            await _db.SaveChangesAsync();

            SuccessMessage = $"Cliente {partner.ID} aggiornato correttamente.";
            Log("UpdateCliente", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowEditModal = true;
            ErrorMessage = $"Errore durante la modifica del cliente: {ex.Message}";
            Log("UpdateClienteError", ex.Message);
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }
    }

    private async Task ValidateCreateInputAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.PartnerType))
        {
            ModelState.AddModelError("Input.PartnerType", "Seleziona un tipo.");
        }
        else if (Input.PartnerType.Length > 1)
        {
            ModelState.AddModelError("Input.PartnerType", "Il tipo non puo superare 1 carattere.");
        }

        if (string.IsNullOrWhiteSpace(Input.ID))
        {
            ModelState.AddModelError("Input.ID", "Il codice cliente e obbligatorio.");
        }
        else if (Input.ID.Length > 50)
        {
            ModelState.AddModelError("Input.ID", "Il codice cliente non puo superare 50 caratteri.");
        }

        ValidateClienteSharedFields("Input", Input.Name, Input.Sector);

        if (ModelState.IsValid)
        {
            var duplicateExists = await _db.Partners
                .AsNoTracking()
                .AnyAsync(x => x.PartnerType == Input.PartnerType && x.ID == Input.ID);

            if (duplicateExists)
            {
                ModelState.AddModelError("Input.ID", "Esiste gia un cliente con questo tipo e codice.");
            }
        }
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

    private async Task ValidateEditInputAsync()
    {
        if (EditInput.Guid == Guid.Empty)
        {
            ModelState.AddModelError("EditInput.Guid", "Cliente non valido.");
        }
        else
        {
            var exists = await _db.Partners
                .AsNoTracking()
                .AnyAsync(x => x.Guid == EditInput.Guid);

            if (!exists)
            {
                ModelState.AddModelError("EditInput.Guid", "Cliente non trovato.");
            }
        }

        ValidateClienteSharedFields("EditInput", EditInput.Name, EditInput.Sector);
    }

    private void ValidateClienteSharedFields(string prefix, string name, int sector)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError($"{prefix}.Name", "La ragione sociale e obbligatoria.");
        }
        else if (name.Length > 50)
        {
            ModelState.AddModelError($"{prefix}.Name", "La ragione sociale non puo superare 50 caratteri.");
        }

        if (sector < 0)
        {
            ModelState.AddModelError($"{prefix}.Sector", "Il settore non puo essere negativo.");
        }
    }

    private async Task LoadPageDataAsync(bool preserveCreateInput = false, bool preserveEditInput = false)
    {
        if (!preserveCreateInput && string.IsNullOrWhiteSpace(Input.PartnerType))
        {
            Input.PartnerType = "C";
        }

        PartnerTypeOptions = await LoadPartnerTypeOptionsAsync();
        Rows = await LoadRowsAsync();

        if (preserveEditInput && EditInput.Guid != Guid.Empty)
        {
            var editPartner = await _db.Partners
                .AsNoTracking()
                .Where(x => x.Guid == EditInput.Guid)
                .Select(x => new { x.PartnerType, x.ID })
                .FirstOrDefaultAsync();

            if (editPartner is not null)
            {
                EditInput.PartnerType = editPartner.PartnerType;
                EditInput.ID = editPartner.ID;
            }
        }
    }

    private async Task<IReadOnlyList<ClienteRowViewModel>> LoadRowsAsync()
    {
        var query = _db.Partners.AsNoTracking().AsQueryable();

        var search = NormalizeLookupFilter(Filters.Search);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = BuildPartnerSuggestionQuery(search);
        }

        if (!string.IsNullOrWhiteSpace(Filters.PartnerType))
        {
            query = query.Where(x => x.PartnerType == Filters.PartnerType);
        }

        if (Filters.Sector.HasValue)
        {
            query = query.Where(x => x.Sector == Filters.Sector.Value);
        }

        var partners = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.ID)
            .Select(x => new
            {
                x.Guid,
                x.PartnerType,
                x.ID,
                x.Name,
                x.Sector
            })
            .ToListAsync();

        var combinationCounts = await _db.Combinations
            .AsNoTracking()
            .GroupBy(x => new { x.PartnerType, x.IDPartner })
            .Select(group => new
            {
                group.Key.PartnerType,
                PartnerID = group.Key.IDPartner,
                Count = group.Count()
            })
            .ToListAsync();

        var locationCounts = await _db.Locations
            .AsNoTracking()
            .GroupBy(x => new { x.PartnerType, x.PartnerID })
            .Select(group => new
            {
                group.Key.PartnerType,
                group.Key.PartnerID,
                Count = group.Count()
            })
            .ToListAsync();

        var combinationsByPartner = combinationCounts.ToDictionary(
            x => BuildPartnerKey(x.PartnerType, x.PartnerID),
            x => x.Count);
        var locationsByPartner = locationCounts.ToDictionary(
            x => BuildPartnerKey(x.PartnerType, x.PartnerID),
            x => x.Count);

        return partners
            .Select(x =>
            {
                var key = BuildPartnerKey(x.PartnerType, x.ID);
                return new ClienteRowViewModel
                {
                    Guid = x.Guid,
                    PartnerType = x.PartnerType,
                    ID = x.ID,
                    Name = x.Name,
                    Sector = x.Sector,
                    CombinationCount = combinationsByPartner.GetValueOrDefault(key),
                    LocationCount = locationsByPartner.GetValueOrDefault(key)
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SelectListItem>> LoadPartnerTypeOptionsAsync()
    {
        var values = await _db.Partners
            .AsNoTracking()
            .Select(x => x.PartnerType)
            .Distinct()
            .ToListAsync();

        values.Add("C");
        if (!string.IsNullOrWhiteSpace(Input.PartnerType))
        {
            values.Add(Input.PartnerType);
        }

        if (!string.IsNullOrWhiteSpace(Filters.PartnerType))
        {
            values.Add(Filters.PartnerType);
        }

        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new SelectListItem
            {
                Value = x,
                Text = x,
                Selected = Input.PartnerType == x
            })
            .ToList();
    }

    private void NormalizeCreateInput()
    {
        Input.PartnerType = Input.PartnerType?.Trim().ToUpperInvariant() ?? string.Empty;
        Input.ID = Input.ID?.Trim() ?? string.Empty;
        Input.Name = Input.Name?.Trim() ?? string.Empty;
    }

    private void NormalizeEditInput()
    {
        EditInput.Name = EditInput.Name?.Trim() ?? string.Empty;
    }

    private static string BuildPartnerKey(string partnerType, string partnerId)
    {
        return $"{partnerType}|{partnerId}";
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

    private static string[] SplitSearchWords(string value)
    {
        return value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private IActionResult RedirectAfterPost(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return LocalRedirect("/Client/Clienti.cshtml");
    }

    private void Log(string functionName, string? text)
    {
        _logger.Log(new LoggerDTO.logDTO
        {
            typeMessage = "Clienti",
            TypeFunction = functionName,
            TypeText = text ?? string.Empty,
            IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
        });
    }
}

public class ClientiFilters
{
    public string? Search { get; set; }

    public string? PartnerType { get; set; }

    public int? Sector { get; set; }
}

public class CreateClienteInput
{
    [Required(ErrorMessage = "Seleziona un tipo.")]
    [StringLength(1, ErrorMessage = "Il tipo non puo superare 1 carattere.")]
    public string PartnerType { get; set; } = "C";

    [Required(ErrorMessage = "Il codice cliente e obbligatorio.")]
    [StringLength(50, ErrorMessage = "Il codice cliente non puo superare 50 caratteri.")]
    public string ID { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ragione sociale e obbligatoria.")]
    [StringLength(50, ErrorMessage = "La ragione sociale non puo superare 50 caratteri.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Il settore non puo essere negativo.")]
    public int Sector { get; set; }
}

public class EditClienteInput
{
    public Guid Guid { get; set; }

    public string PartnerType { get; set; } = string.Empty;

    public string ID { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ragione sociale e obbligatoria.")]
    [StringLength(50, ErrorMessage = "La ragione sociale non puo superare 50 caratteri.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Il settore non puo essere negativo.")]
    public int Sector { get; set; }
}

public class ClienteRowViewModel
{
    public Guid Guid { get; set; }

    public string PartnerType { get; set; } = string.Empty;

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Sector { get; set; }

    public int LocationCount { get; set; }

    public int CombinationCount { get; set; }
}
