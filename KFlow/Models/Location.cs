using System;
using System.Collections.Generic;

namespace KFlow.Models;

public partial class Location
{
    public int LocationID { get; set; }

    public Guid LocationGuid { get; set; }

    public string PartnerType { get; set; } = null!;

    public string PartnerID { get; set; } = string.Empty;

    public string LocationCode { get; set; } = null!;

    public string LocationName { get; set; } = null!;
}
