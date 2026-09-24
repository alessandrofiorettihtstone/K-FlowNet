namespace Scheletro.Models;

public partial class Partner
{
    public Guid Guid { get; set; }

    public string PartnerType { get; set; } = string.Empty;

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Sector { get; set; }
}
