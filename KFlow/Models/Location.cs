namespace Scheletro.Models;

public partial class Location
{
    public int LocationID { get; set; }

    public Guid LocationGuid { get; set; }

    public string PartnerType { get; set; } = string.Empty;

    public string PartnerID { get; set; } = string.Empty;

    public string LocationCode { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;
}
