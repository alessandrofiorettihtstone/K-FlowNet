using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class Status
{
    public short Id { get; set; }

    public Guid Guid { get; set; }

    public string Description { get; set; } = null!;

    public string Color { get; set; } = null!;

    public bool Enabled { get; set; }
}
