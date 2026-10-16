using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Scheletro.Models;

namespace Scheletro.Pages.Mobile;

public class PrelievoModel : PageModel
{
    private readonly dbContext _db;
    private readonly ILogger<PrelievoModel> _logger;
    private readonly string _toPickArchiveDirectory;

    public PrelievoModel(dbContext db, IConfiguration configuration, ILogger<PrelievoModel> logger)
    {
        _db = db;
        _logger = logger;
        _toPickArchiveDirectory = configuration["KFlow:ToPickArchiveDirectory"]
            ?? @"C:\SmartMag\Exchange\Missions\ToPick\IN\Archive";
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string SelectedItemCode { get; private set; } = string.Empty;

    public string SelectedProductionNumber { get; private set; } = string.Empty;

    public IReadOnlyList<PickingTransaction> PickingTransactions { get; private set; } = Array.Empty<PickingTransaction>();

    public class InputModel
    {
        [Required(ErrorMessage = "Inserisci il cartellino.")]
        [StringLength(50)]
        public string CardId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Inserisci numero di picking (PckList).")]
        [StringLength(50)]
        public string ProductionNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Inserisci la quantità.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "La quantità deve essere maggiore di zero.")]
        public double? Quantity { get; set; }
    }

    public async Task OnGetAsync(string? itemCode, string? production, string? cardId)
    {
        // Il cartellino viene passato solo dalla ricerca dello storico. Dopo una
        // registrazione, invece, il form deve restare pronto per un nuovo prelievo.
        if (!string.IsNullOrWhiteSpace(cardId))
        {
            Input.CardId = cardId.Trim();
            Input.ProductionNumber = production?.Trim() ?? string.Empty;
        }

        await LoadPickingTransactionsAsync(itemCode, production);
    }

    public async Task<IActionResult> OnGetCardInfoAsync(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return new JsonResult(new { found = false, message = "Inserisci il cartellino." });
        }

        var product = await (
            from card in _db.Cards.AsNoTracking()
            join combination in _db.Combinations.AsNoTracking() on card.IDCombination equals combination.ID
            join productEntity in _db.Products.AsNoTracking() on combination.IDProduct equals productEntity.ID into products
            from productEntity in products.DefaultIfEmpty()
            where card.IDCard == cardId.Trim()
            select new
            {
                ProductId = combination.IDProduct,
                ProductDescription = productEntity == null ? null : productEntity.Name
            }
        ).SingleOrDefaultAsync();

        return product is null
            ? new JsonResult(new { found = false, message = "Cartellino non trovato." })
            : new JsonResult(new
            {
                found = true,
                productId = product.ProductId,
                productDescription = product.ProductDescription
            });
    }

    public async Task<IActionResult> OnGetProductionInfoAsync(string? production)
    {
        if (string.IsNullOrWhiteSpace(production))
        {
            return new JsonResult(new { found = false, message = "Inserisci il numero di picking di produzione." });
        }

        var productFinished = await GetProductFinishedDataAsync(production.Trim());
        return productFinished is null
            ? new JsonResult(new
            {
                found = false,
                message = "Picking di produzione non trovata o dati ItemPF/CausaQty mancanti."
            })
            : new JsonResult(new { found = true });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.CardId = Input.CardId?.Trim() ?? string.Empty;
        Input.ProductionNumber = Input.ProductionNumber?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var card = await _db.Cards
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.IDCard == Input.CardId);

        if (card is null)
        {
            const string message = "Cartellino non trovato.";
            ErrorMessage = message;
            ModelState.AddModelError(nameof(Input.CardId), message);
            return Page();
        }

        var combination = await _db.Combinations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.ID == card.IDCombination);

        if (combination is null)
        {
            const string message = "La combinazione associata al cartellino non è stata trovata.";
            ErrorMessage = message;
            ModelState.AddModelError(nameof(Input.CardId), message);
            return Page();
        }

        var pf = await GetProductFinishedDataAsync(Input.ProductionNumber);
        if (pf is null)
        {
            const string message = "Produzione non trovata: ItemPF, descrizione o quantità CausaQty non disponibili.";
            ErrorMessage = message;
            ModelState.AddModelError(nameof(Input.ProductionNumber), message);
            return Page();
        }

        _db.PickingTransactions.Add(new PickingTransaction
        {
            IDCard = card.IDCard,
            Production = pf.DocRif,
            Picking = Input.ProductionNumber,
            ItemCode = combination.IDProduct,
            ItemPF = pf.Item,
            DescriptionPF = pf.Description,
            QtyPF = pf.Quantity,
            Operator = GetOperatorDisplayName(),
            Qty = Input.Quantity!.Value,
            CreateDate = DateTime.Now,
            IsEnable = true
        });

        await _db.SaveChangesAsync();

        SuccessMessage = "Prelievo registrato correttamente.";
        return RedirectToPage(new
        {
            itemCode = combination.IDProduct,
            production = Input.ProductionNumber
        });
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, double? quantity, string? itemCode, string? production)
    {
        await LoadPickingTransactionsAsync(itemCode, production);

        if (quantity is null || quantity <= 0)
        {
            ErrorMessage = "Inserisci una quantità numerica maggiore di zero.";
            return RedirectToPage(new { itemCode, production });
        }

        var transaction = await _db.PickingTransactions
            .SingleOrDefaultAsync(x => x.ID == id && x.IsEnable);

        if (transaction is null)
        {
            ErrorMessage = "Movimento di prelievo non trovato.";
            return RedirectToPage(new { itemCode, production });
        }

        transaction.Qty = quantity.Value;
        transaction.LastUpdate = DateTime.Now;
        transaction.LastUpdatedBy = GetOperatorDisplayName();
        await _db.SaveChangesAsync();

        SuccessMessage = "Quantità del prelievo aggiornata correttamente.";
        return RedirectToPage(new { itemCode, production });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? itemCode, string? production)
    {
        var transaction = await _db.PickingTransactions
            .SingleOrDefaultAsync(x => x.ID == id && x.IsEnable);

        if (transaction is null)
        {
            SuccessMessage = "Il movimento non era più disponibile.";
            return RedirectToPage(new { itemCode, production });
        }

        // Manteniamo lo storico: l'eliminazione nasconde il movimento dai risultati attivi.
        transaction.IsEnable = false;
        transaction.LastUpdate = DateTime.Now;
        transaction.LastUpdatedBy = GetOperatorDisplayName();
        await _db.SaveChangesAsync();

        SuccessMessage = "Movimento di prelievo eliminato correttamente.";
        return RedirectToPage(new { itemCode, production });
    }

    private async Task LoadPickingTransactionsAsync(string? itemCode, string? production)
    {
        SelectedItemCode = itemCode?.Trim() ?? string.Empty;
        SelectedProductionNumber = production?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(SelectedItemCode) || string.IsNullOrWhiteSpace(SelectedProductionNumber))
        {
            PickingTransactions = Array.Empty<PickingTransaction>();
            return;
        }

        PickingTransactions = await _db.PickingTransactions
            .AsNoTracking()
            .Where(x => x.IsEnable
                && x.ItemCode == SelectedItemCode
                && x.Picking == SelectedProductionNumber)
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.ID)
            .ToListAsync();
    }

    private async Task<ProductFinishedData?> GetProductFinishedDataAsync(string production)
    {
        var existing = await _db.PickingTransactions
            .AsNoTracking()
            .Where(x => x.Picking == production
                && x.ItemPF != null
                && x.ItemPF != string.Empty
                && x.DescriptionPF != null
                && x.DescriptionPF != string.Empty
                && x.QtyPF.HasValue)
            .Select(x => new { x.ItemPF, x.DescriptionPF, x.QtyPF, x.Production })
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            return new ProductFinishedData(existing.ItemPF!, existing.DescriptionPF, existing.QtyPF, existing.Production);
        }

        return ReadProductFinishedDataFromArchive(production);
    }

    private ProductFinishedData? ReadProductFinishedDataFromArchive(string production)
    {
        if (string.IsNullOrWhiteSpace(production) || !Directory.Exists(_toPickArchiveDirectory))
        {
            return null;
        }

        var productionPrefix = production.Split('_', 2)[0];
        try
        {
            var file = Directory.EnumerateFiles(_toPickArchiveDirectory, $"{productionPrefix}_*.csv")
                .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (file is null)
            {
                return null;
            }

            using var reader = new StreamReader(file);
            var headers = ParseCsvLine(reader.ReadLine() ?? string.Empty);
            var indexes = headers
                .Select((header, index) => new { Header = header.Trim(), index })
                .ToDictionary(x => x.Header, x => x.index, StringComparer.OrdinalIgnoreCase);

            if (!indexes.TryGetValue("CausaRif", out var itemIndex)
                || !indexes.TryGetValue("CausaDesc", out var descriptionIndex)
                || !indexes.TryGetValue("CausaQty", out var quantityIndex)
                || !indexes.TryGetValue("DocRif", out var docrif))
            {
                _logger.LogWarning("Il CSV {File} non contiene tutte le colonne CausaRif/CausaDesc/CausaQty.", file);
                return null;
            }

            while (!reader.EndOfStream)
            {
                var values = ParseCsvLine(reader.ReadLine() ?? string.Empty);
                            if (values.Count <= new[] {
                            itemIndex,
                            descriptionIndex,
                            quantityIndex,
                            docrif
                        }.Max()
                         || string.IsNullOrWhiteSpace(values[itemIndex]))
                {
                    continue;
                }

                var quantity = double.TryParse(
                    values[quantityIndex].Trim(),
                    NumberStyles.Float,
                    CultureInfo.GetCultureInfo("it-IT"),
                    out var parsedQuantity)
                    ? parsedQuantity
                    : (double?)null;

                if (string.IsNullOrWhiteSpace(values[descriptionIndex]) || quantity is null)
                {
                    continue;
                }

                return new ProductFinishedData(
     values[itemIndex].Trim(),
     values[descriptionIndex].Trim(),
     quantity,
     values[docrif].Trim()
 );
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Impossibile leggere il CSV per la produzione {Production}.", production);
        }

        return null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ';' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return values;
    }

    public record ProductFinishedData(
    string Item,
    string? Description,
    double? Quantity,
    string DocRif
);

    private string GetOperatorDisplayName() => string.Join(" ", new[]
    {
        User.FindFirstValue(ClaimTypes.GivenName),
        User.FindFirstValue(ClaimTypes.Surname)
    }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}
