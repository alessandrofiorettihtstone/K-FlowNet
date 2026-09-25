using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class Partner
{
    public Guid Guid { get; set; }

    public string PartnerType { get; set; } = null!;

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = null!;

    public int Sector { get; set; }
}
