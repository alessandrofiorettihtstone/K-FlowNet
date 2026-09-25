using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class Product
{
    public Guid Guid { get; set; }

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = null!;

    public string UMBase { get; set; } = string.Empty;
}
