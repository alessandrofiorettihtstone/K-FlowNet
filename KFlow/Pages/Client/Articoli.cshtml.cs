using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KFlow.Class;
using KFlow.Models;

namespace KFlow.Pages.Client;

[Authorize]
public class ArticoliModel : PageModel
{
    private readonly dbContext _db;
    private readonly LoggerDTO _logger;

    public ArticoliModel(dbContext db, LoggerDTO logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public ArticoliFilters Filters { get; set; } = new();

    [BindProperty]
    public CreateArticoloInput Input { get; set; } = new();

    [BindProperty]
    public EditArticoloInput EditInput { get; set; } = new();

    public IReadOnlyList<ArticoloRowViewModel> Rows { get; private set; } = Array.Empty<ArticoloRowViewModel>();

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

        if (searchType == "A")
        {
            var values = await BuildProductSuggestionQuery(prefixText)
                .OrderBy(x => x.ID)
                .Select(x => $"{x.ID} - {x.Name}")
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        if (searchType == "UM")
        {
            var values = await _db.Products
                .AsNoTracking()
                .Where(x => x.UMBase.Contains(prefixText))
                .OrderBy(x => x.UMBase)
                .Select(x => x.UMBase)
                .Where(x => x != string.Empty)
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(values);
        }

        return new JsonResult(Array.Empty<string>());
    }

    public async Task<IActionResult> OnPostCreateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeCreateInput();

        await ValidateCreateInputAsync();

        if (!ModelState.IsValid)
        {
            ShowCreateModal = true;
            ErrorMessage = "Controlla i dati dell'articolo.";
            await LoadPageDataAsync();
            return Page();
        }

        try
        {
            var product = new Product
            {
                Guid = Guid.NewGuid(),
                ID = Input.ID,
                Name = Input.Name,
                UMBase = Input.UMBase
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Articolo {product.ID} creato correttamente.";
            Log("CreateArticolo", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowCreateModal = true;
            ErrorMessage = $"Errore durante la creazione dell'articolo: {ex.Message}";
            Log("CreateArticoloError", ex.Message);
            await LoadPageDataAsync();
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
            ErrorMessage = "Controlla i dati dell'articolo.";
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }

        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(x => x.Guid == EditInput.Guid);
            if (product is null)
            {
                ShowEditModal = true;
                ErrorMessage = "Articolo non trovato.";
                ModelState.AddModelError("EditInput.Guid", "Articolo non trovato.");
                await LoadPageDataAsync(preserveEditInput: true);
                return Page();
            }

            product.Name = EditInput.Name;
            product.UMBase = EditInput.UMBase;

            await _db.SaveChangesAsync();

            SuccessMessage = $"Articolo {product.ID} aggiornato correttamente.";
            Log("UpdateArticolo", SuccessMessage);

            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowEditModal = true;
            ErrorMessage = $"Errore durante la modifica dell'articolo: {ex.Message}";
            Log("UpdateArticoloError", ex.Message);
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }
    }

    private async Task ValidateCreateInputAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.ID))
        {
            ModelState.AddModelError("Input.ID", "Il codice articolo e obbligatorio.");
        }
        else if (Input.ID.Length > 50)
        {
            ModelState.AddModelError("Input.ID", "Il codice articolo non puo superare 50 caratteri.");
        }

        ValidateArticoloSharedFields("Input", Input.Name, Input.UMBase);

        if (ModelState.IsValid)
        {
            var duplicateExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.ID == Input.ID);

            if (duplicateExists)
            {
                ModelState.AddModelError("Input.ID", "Esiste gia un articolo con questo codice.");
            }
        }
    }

    private IQueryable<Product> BuildProductSuggestionQuery(string prefixText)
    {
        var byID = _db.Products
            .AsNoTracking()
            .Where(x => x.ID.Contains(prefixText));

        var byName = _db.Products.AsNoTracking().AsQueryable();
        foreach (var word in SplitSearchWords(prefixText))
        {
            var searchWord = word;
            byName = byName.Where(x => x.Name.Contains(searchWord));
        }

        return byID.Concat(byName);
    }

    private async Task ValidateEditInputAsync()
    {
        if (EditInput.Guid == Guid.Empty)
        {
            ModelState.AddModelError("EditInput.Guid", "Articolo non valido.");
        }
        else
        {
            var exists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.Guid == EditInput.Guid);

            if (!exists)
            {
                ModelState.AddModelError("EditInput.Guid", "Articolo non trovato.");
            }
        }

        ValidateArticoloSharedFields("EditInput", EditInput.Name, EditInput.UMBase);
    }

    private void ValidateArticoloSharedFields(string prefix, string name, string UMBase)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError($"{prefix}.Name", "La descrizione e obbligatoria.");
        }
        else if (name.Length > 150)
        {
            ModelState.AddModelError($"{prefix}.Name", "La descrizione non puo superare 150 caratteri.");
        }

        if (UMBase.Length > 50)
        {
            ModelState.AddModelError($"{prefix}.UMBase", "L'unita di misura non puo superare 50 caratteri.");
        }
    }

    private async Task LoadPageDataAsync(bool preserveEditInput = false)
    {
        Rows = await LoadRowsAsync();

        if (preserveEditInput && EditInput.Guid != Guid.Empty)
        {
            var editProduct = await _db.Products
                .AsNoTracking()
                .Where(x => x.Guid == EditInput.Guid)
                .Select(x => x.ID)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(editProduct))
            {
                EditInput.ID = editProduct;
            }
        }
    }

    private async Task<IReadOnlyList<ArticoloRowViewModel>> LoadRowsAsync()
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        var search = NormalizeLookupFilter(Filters.Search);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = BuildProductSuggestionQuery(search);
        }

        var unitFilter = NormalizeLookupFilter(Filters.UMBase);
        if (!string.IsNullOrWhiteSpace(unitFilter))
        {
            query = query.Where(x => x.UMBase.Contains(unitFilter));
        }

        var products = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.ID)
            .Select(x => new
            {
                x.Guid,
                x.ID,
                x.Name,
                x.UMBase
            })
            .ToListAsync();

        var combinationCounts = await _db.Combinations
            .AsNoTracking()
            .GroupBy(x => x.IDProduct)
            .Select(group => new
            {
                IDProduct = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(x => x.IDProduct, x => x.Count);

        var cardCounts = await (
            from card in _db.Cards.AsNoTracking()
            join combination in _db.Combinations.AsNoTracking()
                on card.IDCombination equals combination.ID
            group card by combination.IDProduct into groupItems
            select new
            {
                IDProduct = groupItems.Key,
                Count = groupItems.Count()
            })
            .ToDictionaryAsync(x => x.IDProduct, x => x.Count);

        return products
            .Select(x => new ArticoloRowViewModel
            {
                Guid = x.Guid,
                ID = x.ID,
                Name = x.Name,
                UMBase = x.UMBase,
                CombinationCount = combinationCounts.GetValueOrDefault(x.ID),
                CardCount = cardCounts.GetValueOrDefault(x.ID)
            })
            .ToList();
    }

    private void NormalizeCreateInput()
    {
        Input.ID = Input.ID?.Trim() ?? string.Empty;
        Input.Name = Input.Name?.Trim() ?? string.Empty;
        Input.UMBase = Input.UMBase?.Trim() ?? string.Empty;
    }

    private void NormalizeEditInput()
    {
        EditInput.Name = EditInput.Name?.Trim() ?? string.Empty;
        EditInput.UMBase = EditInput.UMBase?.Trim() ?? string.Empty;
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

        return LocalRedirect("/Client/Articoli.cshtml");
    }

    private void Log(string functionName, string? text)
    {
        _logger.Log(new LoggerDTO.logDTO
        {
            typeMessage = "Articoli",
            TypeFunction = functionName,
            TypeText = text ?? string.Empty,
            IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
        });
    }
}

public class ArticoliFilters
{
    public string? Search { get; set; }

    public string? UMBase { get; set; }
}

public class CreateArticoloInput
{
    [Required(ErrorMessage = "Il codice articolo e obbligatorio.")]
    [StringLength(50, ErrorMessage = "Il codice articolo non puo superare 50 caratteri.")]
    public string ID { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descrizione e obbligatoria.")]
    [StringLength(150, ErrorMessage = "La descrizione non puo superare 150 caratteri.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "L'unita di misura non puo superare 50 caratteri.")]
    public string UMBase { get; set; } = string.Empty;
}

public class EditArticoloInput
{
    public Guid Guid { get; set; }

    public string ID { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descrizione e obbligatoria.")]
    [StringLength(150, ErrorMessage = "La descrizione non puo superare 150 caratteri.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "L'unita di misura non puo superare 50 caratteri.")]
    public string UMBase { get; set; } = string.Empty;
}

public class ArticoloRowViewModel
{
    public Guid Guid { get; set; }

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string UMBase { get; set; } = string.Empty;

    public int CombinationCount { get; set; }

    public int CardCount { get; set; }
}
