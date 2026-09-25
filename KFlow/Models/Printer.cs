using System;
using System.Collections.Generic;

namespace Scheletro.Models;

public partial class Printer
{
    public int IdPrinter { get; set; }

    public string Ip { get; set; } = null!;

    public int Port { get; set; }
}
