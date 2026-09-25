namespace Scheletro.ViewModels;

public class KanbanFilters
{
    public string? CardId { get; set; }

    public string? Partner { get; set; }

    public string? Product { get; set; }

    public List<short> StatusIds { get; set; } = new();
}

public class KanbanBoardViewModel
{
    public IReadOnlyList<StatusOptionViewModel> Statuses { get; set; } = Array.Empty<StatusOptionViewModel>();

    public IReadOnlyList<KanbanStatusGroupViewModel> Groups { get; set; } = Array.Empty<KanbanStatusGroupViewModel>();

    public KanbanFilters Filters { get; set; } = new();
}

public class StatusOptionViewModel
{
    public short ID { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public bool Selected { get; set; }
}

public class KanbanStatusGroupViewModel
{
    public short StatusId { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public IReadOnlyList<PartnerProductRowViewModel> Rows { get; set; } = Array.Empty<PartnerProductRowViewModel>();
}

public class PartnerProductRowViewModel
{
    public int IDCombinazione { get; set; }

    public string IDPartner { get; set; } = string.Empty;

    public string PartnerName { get; set; } = string.Empty;

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }

    public string IDProduct { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string IDCross { get; set; } = string.Empty;

    public IReadOnlyList<CardInfoViewModel> Cards { get; set; } = Array.Empty<CardInfoViewModel>();
}

public class CardInfoViewModel
{
    public string IDCard { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

public class CardDetailViewModel
{
    public string IDCard { get; set; } = string.Empty;

    public short Status { get; set; }

    public string StatusDescription { get; set; } = string.Empty;

    public string StatusColor { get; set; } = string.Empty;

    public string IDPartner { get; set; } = string.Empty;

    public string PartnerName { get; set; } = string.Empty;

    public string IDProduct { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string IDCross { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string UMBase { get; set; } = string.Empty;

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }
}
