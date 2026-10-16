using System;
using System.Collections.Generic;

namespace KFlow.Models;

public partial class Card
{
    public Guid Guid { get; set; }

    public string IDCard { get; set; } = string.Empty;

    public int IDCombination { get; set; }

    public int Quantity { get; set; }

    public short Status { get; set; }

    public string? StorageLocation { get; set; }
}
