using Microsoft.EntityFrameworkCore;
using Scheletro.Models;
using Scheletro.ViewModels;

namespace Scheletro.Services;

public class KanbanService
{
    private readonly dbContext _db;

    public KanbanService(dbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<KFlowStatus>> GetEnabledStatusesAsync()
    {
        return await _db.Statuses
            .AsNoTracking()
            .Where(x => x.Enabled)
            .OrderBy(x => x.ID)
            .ToListAsync();
    }

    public async Task<KanbanBoardViewModel> GetBoardAsync(KanbanFilters filters)
    {
        var statuses = await GetEnabledStatusesAsync();
        var enabledStatusIds = statuses.Select(x => x.ID).ToHashSet();
        var selectedStatusIds = filters.StatusIds
            .Where(enabledStatusIds.Contains)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (selectedStatusIds.Count == 0)
        {
            selectedStatusIds = statuses.Select(x => x.ID).ToList();
        }

        var query = _db.VwKanbanBoard
            .AsNoTracking()
            .Where(x => selectedStatusIds.Contains(x.Status));

        var cardFilter = filters.CardId?.Trim();
        if (!string.IsNullOrWhiteSpace(cardFilter))
        {
            query = query.Where(x => x.IDCard.Contains(cardFilter));
        }

        var partnerFilter = NormalizeLookupFilter(filters.Partner);
        if (!string.IsNullOrWhiteSpace(partnerFilter))
        {
            query = query.Where(x => x.IDPartner.Contains(partnerFilter) || x.PartnerName.Contains(partnerFilter));
        }

        var productFilter = NormalizeLookupFilter(filters.Product);
        if (!string.IsNullOrWhiteSpace(productFilter))
        {
            query = query.Where(x => x.IDProduct.Contains(productFilter) || x.ProductName.Contains(productFilter));
        }

        var rows = await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.PartnerName)
            .ThenBy(x => x.ProductName)
            .ThenBy(x => x.IDCard)
            .ToListAsync();

        var rowsByStatus = rows
            .GroupBy(x => x.Status)
            .ToDictionary(x => x.Key, x => x.ToList());

        var groups = selectedStatusIds
            .Select(statusId =>
            {
                var status = statuses.First(x => x.ID == statusId);
                var statusRows = rowsByStatus.GetValueOrDefault(statusId) ?? new List<VwKanbanBoard>();

                return new KanbanStatusGroupViewModel
                {
                    StatusId = status.ID,
                    StatusName = status.Description,
                    Color = NormalizeColor(status.Color),
                    Rows = statusRows
                        .GroupBy(x => new
                        {
                            x.IDCombinazione,
                            x.IDPartner,
                            x.PartnerName,
                            x.IDProduct,
                            x.ProductName,
                            x.IDCross
                        })
                        .Select(group => new PartnerProductRowViewModel
                        {
                            IDCombinazione = group.Key.IDCombinazione,
                            IDPartner = group.Key.IDPartner,
                            PartnerName = group.Key.PartnerName,
                            IDProduct = group.Key.IDProduct,
                            ProductName = group.Key.ProductName,
                            IDCross = group.Key.IDCross,
                            Cards = group
                                .Select(card => new CardInfoViewModel
                                {
                                    IDCard = card.IDCard,
                                    Quantity = card.Quantity
                                })
                                .OrderBy(card => card.IDCard)
                                .ToList()
                        })
                        .ToList()
                };
            })
            .ToList();

        return new KanbanBoardViewModel
        {
            Filters = filters,
            Statuses = statuses
                .Select(status => new StatusOptionViewModel
                {
                    ID = status.ID,
                    Description = status.Description,
                    Color = NormalizeColor(status.Color),
                    Selected = selectedStatusIds.Contains(status.ID)
                })
                .ToList(),
            Groups = groups
        };
    }

    public async Task<CardDetailViewModel?> GetCardDetailAsync(string idCard)
    {
        var card = await _db.VwKanbanBoard
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IDCard == idCard);

        return card == null ? null : ToCardDetail(card);
    }

    public async Task ChangeCardStatusAsync(string idCard, short newStatus)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(x => x.IDCard == idCard);
        if (card == null)
        {
            throw new InvalidOperationException("Cartellino non trovato.");
        }

        card.Status = newStatus;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateCardAsync(string idCard, int quantity, short status)
    {
        if (string.IsNullOrWhiteSpace(idCard))
        {
            throw new InvalidOperationException("Cartellino non valido.");
        }

        if (quantity <= 0)
        {
            throw new InvalidOperationException("La quantita deve essere maggiore di zero.");
        }

        var statusEnabled = await _db.Statuses
            .AsNoTracking()
            .AnyAsync(x => x.ID == status && x.Enabled);

        if (!statusEnabled)
        {
            throw new InvalidOperationException("Lo stato selezionato non esiste o non e abilitato.");
        }

        var card = await _db.Cards.FirstOrDefaultAsync(x => x.IDCard == idCard.Trim());
        if (card == null)
        {
            throw new InvalidOperationException("Cartellino non trovato.");
        }

        card.Quantity = quantity;
        card.Status = status;
        await _db.SaveChangesAsync();
    }

    public async Task ChangeCombinationStatusAsync(int combinationId, short newStatus)
    {
        var cards = await _db.Cards
            .Where(x => x.IDCombination == combinationId)
            .ToListAsync();

        foreach (var card in cards)
        {
            card.Status = newStatus;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> ChangeCardStatusFromMobileAsync(string idCard, short newStatus)
    {
        if (newStatus > 300 && newStatus < 700)
        {
            return (false, "Nuovo stato non autorizzato.");
        }

        var card = await _db.Cards.FirstOrDefaultAsync(x => x.IDCard == idCard);
        if (card == null)
        {
            return (false, "Cartellino non trovato.");
        }

        if (card.Status == 300 && newStatus < 300)
        {
            return (false, "Nuovo stato non autorizzato.");
        }

        if (newStatus != 700 && newStatus != 100 && Math.Abs(card.Status - newStatus) > 100)
        {
            return (false, "Cambio stato non puo essere maggiore di uno step.");
        }

        card.Status = newStatus;
        await _db.SaveChangesAsync();
        return (true, "Stato aggiornato.");
    }

    public async Task<IReadOnlyList<string>> GetPartnerSuggestionsAsync(string term, int count = 10)
    {
        term = term.Trim();
        return await _db.VwKanbanBoard
            .AsNoTracking()
            .Where(x => x.IDPartner.Contains(term) || x.PartnerName.Contains(term))
            .OrderBy(x => x.PartnerName)
            .Select(x => x.IDPartner + " - " + x.PartnerName)
            .Distinct()
            .Take(count)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetProductSuggestionsAsync(string term, int count = 10)
    {
        term = term.Trim();
        return await _db.VwKanbanBoard
            .AsNoTracking()
            .Where(x => x.IDProduct.Contains(term) || x.ProductName.Contains(term))
            .OrderBy(x => x.ProductName)
            .Select(x => x.IDProduct + " - " + x.ProductName)
            .Distinct()
            .Take(count)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetCardSuggestionsAsync(string term, int count = 10)
    {
        term = term.Trim();
        return await _db.VwKanbanBoard
            .AsNoTracking()
            .Where(x => x.IDCard.StartsWith(term))
            .OrderBy(x => x.IDCard)
            .Select(x => x.IDCard)
            .Distinct()
            .Take(count)
            .ToListAsync();
    }

    private static CardDetailViewModel ToCardDetail(VwKanbanBoard card)
    {
        return new CardDetailViewModel
        {
            IDCard = card.IDCard,
            Status = card.Status,
            StatusDescription = card.StatusDescription,
            StatusColor = NormalizeColor(card.Color),
            IDPartner = card.IDPartner,
            PartnerName = card.PartnerName,
            IDProduct = card.IDProduct,
            ProductName = card.ProductName,
            IDCross = card.IDCross,
            Quantity = card.Quantity,
            UMBase = card.UMBase,
            LocationCode = card.LocationCode,
            LocationName = card.LocationName
        };
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

    public static string NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
        {
            return "#6c757d";
        }

        return value;
    }
}
