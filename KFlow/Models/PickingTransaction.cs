using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class PickingTransaction
{
    public int ID { get; set; }

    public string Production { get; set; } = null!;

    public string IDCard { get; set; } = string.Empty;

    public string ItemCode { get; set; } = string.Empty;

    public string? ItemPF { get; set; }

    public string? DescriptionPF { get; set; }

    public double? QtyPF { get; set; }

    public string Operator { get; set; } = string.Empty;

    public double Qty { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? LastUpdate { get; set; }

    public string LastUpdatedBy { get; set; } = string.Empty;

    public bool IsEnable { get; set; }
}
