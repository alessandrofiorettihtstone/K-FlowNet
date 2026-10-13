using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Scheletro.Models;

namespace Scheletro.Pages.Mobile;

public class PrelievoModel : PageModel
{
    private readonly dbContext _db;

    public PrelievoModel(dbContext db)
    {
        _db = db;
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

        [Required(ErrorMessage = "Inserisci il numero di produzione.")]
        [StringLength(50)]
        public string ProductionNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Inserisci la quantità.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "La quantità deve essere maggiore di zero.")]
        public double? Quantity { get; set; }
    }

    public async Task OnGetAsync(string? itemCode, string? production, string? cardId)
    {
        Input.CardId = cardId?.Trim() ?? string.Empty;
        Input.ProductionNumber = production?.Trim() ?? string.Empty;
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
            ModelState.AddModelError(nameof(Input.CardId), "Cartellino non trovato.");
            return Page();
        }

        var combination = await _db.Combinations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.ID == card.IDCombination);

        if (combination is null)
        {
            ModelState.AddModelError(nameof(Input.CardId), "La combinazione associata al cartellino non è stata trovata.");
            return Page();
        }

        _db.PickingTransactions.Add(new PickingTransaction
        {
            IDCard = card.IDCard,
            Production = Input.ProductionNumber,
            ItemCode = combination.IDProduct,
            ItemPF = combination.IDCross,
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
                && x.Production == SelectedProductionNumber)
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.ID)
            .ToListAsync();
    }
}
