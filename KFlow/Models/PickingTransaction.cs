using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class PickingTransaction
{
    public int ID { get; set; }

    public string Production { get; set; } = null!;

    public string IDCard { get; set; } = string.Empty;

    public string ItemCode { get; set; } = string.Empty;

    public string ItemPF { get; set; } = string.Empty;

    public double Qty { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? LastUpdate { get; set; }

    public bool IsEnable { get; set; }
}
