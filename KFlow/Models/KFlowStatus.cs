namespace Scheletro.Models;

public partial class KFlowStatus
{
    public short ID { get; set; }

    public Guid Guid { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}
