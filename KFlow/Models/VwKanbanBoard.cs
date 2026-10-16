using System;
using System.Collections.Generic;

namespace KFlow.Models;

public partial class VwKanbanBoard
{
    public Guid Guid { get; set; }

    public string IDCard { get; set; } = string.Empty;

    public short Status { get; set; }

    public int IDCombinazione { get; set; }

    public string IDPartner { get; set; } = string.Empty;

    public string PartnerName { get; set; } = null!;

    public string IDProduct { get; set; } = string.Empty;

    public string ProductName { get; set; } = null!;

    public int Quantity { get; set; }

    public string UMBase { get; set; } = string.Empty;

    public string StatusDescription { get; set; } = null!;

    public string Color { get; set; } = null!;

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }

    public string IDCross { get; set; } = string.Empty;
}
