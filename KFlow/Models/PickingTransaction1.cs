using System;
using System.Collections.Generic;

namespace KFlow.Models;

public partial class PickingTransaction1
{
    public int Id { get; set; }

    public string Production { get; set; } = null!;

    public string Idcard { get; set; } = null!;

    public string ItemCode { get; set; } = null!;

    public string ItemPf { get; set; } = null!;

    public double Qty { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? LastUpdate { get; set; }

    public bool IsEnable { get; set; }
}
