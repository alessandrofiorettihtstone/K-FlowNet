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
        return RedirectToPage();
    }
}
