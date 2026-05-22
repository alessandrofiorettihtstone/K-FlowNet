namespace Scheletro.Models;

public partial class Printer
{
    public int IdPrinter { get; set; }

    public string Ip { get; set; } = string.Empty;

    public int Port { get; set; }
}
