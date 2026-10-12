using System;
using System.Collections.Generic;

namespace KFlow.Models;

public partial class Counter
{
    public string Idcounter { get; set; } = null!;

    public long LastValue { get; set; }
}
