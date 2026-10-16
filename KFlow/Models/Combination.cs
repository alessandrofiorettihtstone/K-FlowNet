using System;
using System.Collections.Generic;

namespace KFlow.Models;

public partial class Combination
{
    public int ID { get; set; }

    public Guid Guid { get; set; }

    public string PartnerType { get; set; } = null!;

    public string IDPartner { get; set; } = string.Empty;

    public int LocationID { get; set; }

    public string IDProduct { get; set; } = string.Empty;

    public string IDCross { get; set; } = string.Empty;

    public int PDR { get; set; }

    public int MOQ { get; set; }
}
