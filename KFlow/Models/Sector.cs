namespace Scheletro.Models;

public partial class Sector
{
    public int SectorID { get; set; }

    public Guid SectorGuid { get; set; }

    public string SectorName { get; set; } = string.Empty;
}
